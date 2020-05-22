using System;
using System.IO;

namespace VersionDataAnalyzer
{
    public class SummaryData
    {
        public ulong LineCount;
        public ulong NearMissCount;
        public ulong NotSemverCount;
        public ulong SemverCount;

        public SummaryData()
        {
            LineCount = 0;
            NearMissCount = 0;
            NotSemverCount = 0;
            SemverCount = 0;
        }

        public void Write()
        {
            Write(Console.Out);
        }

        public void Write(TextWriter tw)
        {
            float nearMissPercent = ((float)NearMissCount / (float)LineCount) * 100;
            float notSemverPercent = ((float)NotSemverCount / (float)LineCount) * 100;
            float semverPercent = ((float)SemverCount / (float)LineCount) * 100;

            tw.WriteLine("\nSummary");
            tw.WriteLine("--------------------------------------------");
            tw.WriteLine($"Line Count:               {LineCount}");
            tw.WriteLine($"Near misses ('v' prefix): {NearMissCount} ({nearMissPercent}%)");
            tw.WriteLine($"Not SemVer Count:         {NotSemverCount} ({notSemverPercent}%)");
            tw.WriteLine($"SemVer Count:             {SemverCount} ({semverPercent}%)");
            tw.WriteLine();
        }
    }

}