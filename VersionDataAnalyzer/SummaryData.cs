using System;
using System.Diagnostics;
using System.IO;

namespace VersionDataAnalyzer
{
    public class SummaryData
    {
        private DateTime _timeOfReport;
        private readonly FileInfo _inputFile;

        private ulong _versionLengthAccumulator = 0;
        private ulong _maxLength = 0;

        private string _badLinesPathFileName;
        private string _cleanedNearMissPathFileName;
        private string _outputPathFileName;

        public ulong LineCount = 0;
        public ulong NearMissCount = 0;
        public ulong NotSemverCount = 0;
        public ulong SemverCount = 0;
        public ulong NumericVersionCount = 0;
        public ulong AlphaNumericVersionCount = 0;

        public SummaryData(
            FileInfo inputFile, 
            string badLinesPathFileName, 
            string cleanedNearMissPathFileName,
            string outputPathFileName)
        {
            _inputFile = inputFile;
            _badLinesPathFileName = badLinesPathFileName;
            _cleanedNearMissPathFileName = cleanedNearMissPathFileName;
            _outputPathFileName = outputPathFileName;
        }

        public void AddSemVerLength(int length)
        {
            _versionLengthAccumulator += (ulong) length;
            if ((ulong)length > _maxLength) _maxLength = (ulong)length;
        }

        public void Write()
        {
            Write(Console.Out);
        }

        public void Write(TextWriter tw)
        {
            _timeOfReport = DateTime.Now;

            float nearMissPercent = ((float)NearMissCount / (float)LineCount) * 100;
            float notSemverPercent = ((float)NotSemverCount / (float)LineCount) * 100;
            float semverPercent = ((float)SemverCount / (float)LineCount) * 100;

            ulong avgLineLength = _versionLengthAccumulator / LineCount;

            var badLinesFileInfo = new FileInfo(_badLinesPathFileName);
            var cleanedFileInfo = new FileInfo(_cleanedNearMissPathFileName);
            var outputFileInfo = new FileInfo(_outputPathFileName);

            tw.WriteLine("\nSummary");
            tw.WriteLine("----------------------------------------------");
            tw.WriteLine($"Date and time:            {_timeOfReport.ToString()}");
            tw.WriteLine($"Input file:               {_inputFile.FullName} ({_inputFile.CreationTimeUtc.ToString("yyyy-MM-dd-HH-mm-ss")})");
            tw.WriteLine($"Line count:               {LineCount}");
            tw.WriteLine($"Near misses ('v' prefix): {NearMissCount} ({nearMissPercent}%)");
            tw.WriteLine($"Not semver count:         {NotSemverCount} ({notSemverPercent}%)");
            tw.WriteLine($"Semver count:             {SemverCount} ({semverPercent}%)");
            
            tw.WriteLine($"Average line length:      {avgLineLength}");
            tw.WriteLine($"Maximum line length:      {_maxLength}");

            tw.WriteLine($"Output file:              {_outputPathFileName} ({outputFileInfo.Length})");
            tw.WriteLine($"Bad lines file:           {_badLinesPathFileName} ({badLinesFileInfo.Length})");
            tw.WriteLine($"Cleaned near miss file:   {_cleanedNearMissPathFileName} ({cleanedFileInfo.Length})");

            tw.WriteLine();
        }
    }

}