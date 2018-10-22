using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Job.OrderbookToDiskWriter.Core.Domain.Models;
using Lykke.Job.OrderbookToDiskWriter.Core.Services;

namespace Lykke.Job.OrderbookToDiskWriter.Services
{
    public class DataProcessor : TimerPeriod, IDataProcessor
    {
        private const string _directoryFormat = "yyyy-MM-dd-HH";
        private const int _gigabyte = 1024 * 1024 * 1024;

        private readonly ILog _log;
        private readonly IDiskWorker _diskWorker;
        private readonly string _diskPath;
        private readonly int _warningSizeInGigabytes;
        private readonly int _maxSizeInGigabytes;
        private readonly DirectoryInfo _dirInfo;
        private readonly HashSet<string> _directoriesHash = new HashSet<string>();

        public DataProcessor(
            IDiskWorker diskWorker,
            ILog log,
            IShutdownManager shutdownManager,
            string diskPath,
            int warningSizeInGigabytes,
            int maxSizeInGigabytes)
            : base((int)TimeSpan.FromMinutes(90).TotalMilliseconds, log)
        {
            _diskWorker = diskWorker;
            _log = log;
            _diskPath = diskPath;
            _warningSizeInGigabytes = warningSizeInGigabytes > 0 ? warningSizeInGigabytes : 0;
            _maxSizeInGigabytes = maxSizeInGigabytes > 0 ? maxSizeInGigabytes : 0;

            shutdownManager.Register(this, 3);

            if (!Directory.Exists(_diskPath))
                Directory.CreateDirectory(_diskPath);

            _dirInfo = new DirectoryInfo(_diskPath);
            Directory.SetCurrentDirectory(_diskPath);
        }

        public void Process(Orderbook item)
        {
            string directory1 = $"{item.AssetPair.Replace('|', '_')}-{(item.IsBuy ? "buy" : "sell")}";
            if (!_directoriesHash.Contains(directory1))
            {
                if (!Directory.Exists(directory1))
                    Directory.CreateDirectory(directory1);
                _directoriesHash.Add(directory1);
            }
            string directory2 = item.Timestamp.ToString(_directoryFormat);
            var dirPath = Path.Combine(directory1, directory2);

            var convertedText = FormatMessage(item);

            _diskWorker.AddDataItem(convertedText, dirPath);
        }

        public override Task Execute()
        {
            if (_warningSizeInGigabytes == 0 && _maxSizeInGigabytes == 0)
                return Task.CompletedTask;

            var dirFilesCountDict = new Dictionary<int, List<List<FileInfo>>>();
            long totalSize = 0;
            var dirs = _dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var dir in dirs)
            {
                try
                {
                    var files = dir.GetFiles().ToList();
                    totalSize += files.Sum(f => f.Length);
                    if (dirFilesCountDict.ContainsKey(files.Count))
                        dirFilesCountDict[files.Count].Add(files);
                    else
                        dirFilesCountDict.Add(files.Count, new List<List<FileInfo>> { files });
                }
                catch
                {
                }
            }

            int gbSize = (int)(totalSize / _gigabyte);

            if (_warningSizeInGigabytes > 0 && gbSize >= _warningSizeInGigabytes)
                _log.WriteWarning(
                    "DataProcessor.Execute",
                    "SpaceIssue",
                    $"RabbitMq data on {_diskPath} have taken {gbSize}Gb (>= {_warningSizeInGigabytes}Gb)");

            if (_maxSizeInGigabytes == 0 || gbSize < _maxSizeInGigabytes)
                return Task.CompletedTask;

            long sizeToFree = totalSize - _maxSizeInGigabytes * _gigabyte;
            int deletedFilesCount = 0;
            var keys = dirFilesCountDict.Keys.OrderByDescending(k => k).ToList();
            for (int i = 0; i < keys.Count; ++i)
            {
                if (i > 0)
                    dirFilesCountDict[keys[i]].AddRange(dirFilesCountDict[keys[i-1]]);
                var dirsToClean = dirFilesCountDict[keys[i]];
                foreach (var dirFiles in dirsToClean)
                {
                    if (dirFiles.Count == 0)
                        continue;

                    FileInfo file = dirFiles[0];
                    try
                    {
                        if (!File.Exists(file.FullName))
                            continue;
                        File.Delete(file.FullName);
                        _log.WriteWarning("DataProcessor.Execute", "Deleted", $"Deleted {file.FullName} to free some space!");
                        ++deletedFilesCount;
                        sizeToFree -= file.Length;
                        if (sizeToFree <= 0)
                            break;
                    }
                    catch (Exception ex)
                    {
                        _log.WriteWarning(nameof(DataProcessor), nameof(Execute), $"Couldn't delete {file.Name}", ex);
                    }
                    dirFiles.RemoveAt(0);
                }
                if (sizeToFree <= 0)
                    break;
            }
            if (deletedFilesCount > 0)
                _log.WriteWarning("DataProcessor.Execute", "DeletedTotal", $"Deleted {deletedFilesCount} files from {_diskPath}");

            return Task.CompletedTask;
        }

        private static string FormatMessage(Orderbook item)
        {
            var strBuilder = new StringBuilder("{\"t\":\"");
            strBuilder.Append(item.Timestamp.ToString("mm:ss.fff"));
            strBuilder.Append("\",\"p\":[");
            for (int i = 0; i < item.Prices.Count; ++i)
            {
                var price = item.Prices[i];
                if (i > 0)
                    strBuilder.Append(",");
                strBuilder.Append("{\"v\":");
                strBuilder.Append(price.Volume);
                strBuilder.Append(",\"p\":");
                strBuilder.Append(price.Price);
                strBuilder.Append("}");
            }
            strBuilder.Append("]}");

            return strBuilder.ToString();
        }
    }
}
