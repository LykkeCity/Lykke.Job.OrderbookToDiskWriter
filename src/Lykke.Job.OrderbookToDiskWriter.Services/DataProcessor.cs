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
            string directory1 = $"{item.AssetPair}-{(item.IsBuy ? "buy" : "sell")}";
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

        public override async Task Execute()
        {
            if (_warningSizeInGigabytes == 0 && _maxSizeInGigabytes == 0)
                return;

            var fileInfos = _dirInfo.EnumerateFiles("*", SearchOption.AllDirectories);
            long totalSize = fileInfos.Sum(f => f.Length);
            int gbSize = (int)(totalSize / _gigabyte);

            if (_warningSizeInGigabytes > 0 && gbSize >= _warningSizeInGigabytes)
                await _log.WriteWarningAsync(
                    nameof(DataProcessor),
                    nameof(Execute),
                    $"RabbitMq data on {_diskPath} have taken {gbSize}Gb (>= {_warningSizeInGigabytes}Gb)");

            if (_maxSizeInGigabytes == 0 || gbSize < _maxSizeInGigabytes)
                return;

            long sizeToFree = totalSize - _maxSizeInGigabytes * _gigabyte;
            int deletedFilesCount = 0;
            foreach (var file in fileInfos)
            {
                try
                {
                    if (!File.Exists(file.FullName))
                        continue;
                    File.Delete(file.FullName);
                    await _log.WriteWarningAsync(nameof(DataProcessor), nameof(Execute), $"Deleted {file.FullName} to free some space!");
                    sizeToFree -= file.Length;
                    ++deletedFilesCount;
                    if (sizeToFree <= 0)
                        break;
                }
                catch (Exception ex)
                {
                    await _log.WriteWarningAsync(nameof(DataProcessor), nameof(Execute), $"Couldn't delete {file.Name}", ex);
                }
            }
            if (deletedFilesCount > 0)
                await _log.WriteWarningAsync(nameof(DataProcessor), nameof(Execute), $"Deleted {deletedFilesCount} files from {_diskPath}");
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
