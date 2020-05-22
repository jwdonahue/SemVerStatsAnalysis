// Copyright (c) Joseph W Donahue
//
// Licensed under the terms of the MIT license (https://opensource.org/licenses/MIT). See LICENSE.TXT.
//
//  Contact: coders@sharperhacks.com
// ---------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VersionDataAnalyzer
{

    // Prototype code.  
    //
    // I knocked this out to process some data I got from the folks working the
    // https://github.com/theory/pg-semver project.  I wanted to know the
    // distribution of numbers of characters in each of the fields, as well as
    // the distribution of identifiers in the prerelease and meta tags.  This
    // information can be used to tune implementations of SemVer support in
    // various environments, rather than making assumptions regarding storage
    // and performance.

    // This code expects arg1 to specify an input file.
    // Input file should be csv, no quotes, no leading white space,
    // with up to 5 fields:
    //
    //  count,major,minor,patch,optionalPrerelease,optionalMeta
    //
    // This program came about as an expedience, after hand messaging the first
    // data set and finding OpenOffice Calc to be less than desired.
    //
    // I can/will make this better.

    // TODO: Add output file argument.
    // TODO: Add support to read a list of raw semver strings.

    class Program
    {
        private const uint _tripleCharCountSlots = 100;
        private const uint _prereleaseCharCountSlots = 256;
        private const uint _prereleaseFieldCountSlots = 64;
        private const uint _metaCharCountSlots = 256;
        private const uint _metaFieldCountSlots = 64;


        private const char _dot = '.';

        // This regex borrowed from https://github.com/semver/semver/blob/master/semver.md#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
        private const string _semverRegexStr = @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";
        private const string _countAndSemverRegexStr = @"(?<Count>\d*),((?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?)$";

        private static Regex _semverRegex = new Regex(_semverRegexStr, RegexOptions.Compiled);

        private static Regex _countAndSemverRegex = new Regex(_countAndSemverRegexStr, RegexOptions.Compiled);

        static int Main(string[] args)
        {
            SummaryData summary = new SummaryData();

            // TODO: Counts class.
            var majorCounts = new ulong[_tripleCharCountSlots];
            var minorCounts = new ulong[_tripleCharCountSlots];
            var patchCounts = new ulong[_tripleCharCountSlots];
            var prereleaseCharCounts = new ulong[_prereleaseCharCountSlots];
            var prereleaseFieldCounts = new ulong[256];
            var metaCharCounts = new ulong[_metaCharCountSlots];
            var metaFieldCounts = new ulong[256];

            ulong hasPrereleaseCount = 0;
            ulong hasMetaCount = 0;

            // TODO: Arg handler.
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: VersionDataAnalyzer input_file\n");
                return -1;
            }

            string[] lines = System.IO.File.ReadAllLines(args[0]);

            // TODO: Add counter to file names so we don't overwrite data.

            using (StreamWriter badLinesFile = new StreamWriter("BadLines.txt", false, Encoding.ASCII))
            {
                using (StreamWriter nearMisFile = new StreamWriter("CleanedNearMisses.txt", false, Encoding.ASCII))
                {
                    for (long idx = 0; idx < lines.LongLength; idx++)
                    {
                        summary.LineCount++;

                        Match match = _countAndSemverRegex.Match(lines[idx]);

                        UInt64 count = 0;

                        if (match.Success)
                        {
                            count = UInt64.Parse(match.Groups["Count"].Value);
                        }
                        else
                        {
                            match = _semverRegex.Match(lines[idx]);
                        }

                        if (!match.Success)
                        {
                            summary.NotSemverCount++;

                            // TODO: Write this to an error log instead and add an ignore non-SemVer switch.
                            //Console.WriteLine($"ERROR: Failed to match line #{idx + 1}: {lines[idx]}\nContinuing to process file...");

                            char firstChar = lines[idx][0];
                            if (('v' == firstChar) || ('V' == firstChar))
                            {
                                string prefixStripped = lines[idx].Substring(1); //match.Groups["Semver"].Value.Substring(1);
                                match = _semverRegex.Match(prefixStripped);
                                if (match.Success)
                                {
                                    summary.NearMissCount++;
                                    nearMisFile.WriteLine(prefixStripped);
                                }
                            }
                            else
                            {
                                badLinesFile.WriteLine(lines[idx]);
                            }

                            continue;
                        }

                        summary.SemverCount++;

                        // TODO: LimitsData class.
                        ulong majorLengthExcessiveCount = 0;
                        ulong minorLengthExcessiveCount = 0;
                        ulong patchLengthExcessiveCount = 0;
                        ulong prereleaseLengthExcessiveCount = 0;
                        ulong prereleaseFieldsExcessiveCount = 0;
                        ulong metaLengthExcessiveCount = 0;
                        ulong metaFieldsExcessiveCount = 0;

                        int majorLength = match.Groups["major"].Value.Length;
                        int minorLength = match.Groups["minor"].Value.Length;
                        int patchLength = match.Groups["patch"].Value.Length;

                        SetCharFieldCounts(majorLength, _tripleCharCountSlots, count, ref majorCounts[majorLength],
                            ref majorLengthExcessiveCount);
                        SetCharFieldCounts(minorLength, _tripleCharCountSlots, count, ref minorCounts[minorLength],
                            ref minorLengthExcessiveCount);
                        SetCharFieldCounts(patchLength, _tripleCharCountSlots, count, ref patchCounts[patchLength],
                            ref patchLengthExcessiveCount);

                        string prerelease = match.Groups["prerelease"].Value;
                        if (!string.IsNullOrEmpty(prerelease))
                        {
                            SetTagFieldCounts(
                                ref prerelease,
                                ref hasPrereleaseCount,
                                _prereleaseCharCountSlots,
                                count,
                                ref prereleaseCharCounts,
                                ref prereleaseLengthExcessiveCount,
                                _prereleaseFieldCountSlots,
                                ref prereleaseFieldCounts,
                                ref prereleaseFieldsExcessiveCount);
                        }

                        string meta = match.Groups["buildmetadata"].Value;
                        if (!string.IsNullOrEmpty(meta))
                        {
                            SetTagFieldCounts(
                                ref meta,
                                ref hasMetaCount,
                                _metaCharCountSlots,
                                count,
                                ref metaCharCounts,
                                ref metaLengthExcessiveCount,
                                _metaFieldCountSlots,
                                ref metaFieldCounts,
                                ref metaFieldsExcessiveCount
                            );
                        }
                    }

                    Console.WriteLine("Char Count,Major,Minor,Patch,Prerelease,PrereleaseFields,Meta,MetaFields");
                    for (uint idx = 0; idx < _tripleCharCountSlots; idx++)
                    {
                        bool hasCounts = (0 != majorCounts[idx]) ||
                                         (0 != minorCounts[idx]) ||
                                         (0 != patchCounts[idx]) ||
                                         (0 != prereleaseCharCounts[idx]) ||
                                         (0 != metaCharCounts[idx]);

                        if (hasCounts)
                        {
                            Console.WriteLine(
                                $"{idx},{majorCounts[idx]},{minorCounts[idx]},{patchCounts[idx]},{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
                        }
                    }

                    for (uint idx = 100; idx < 256; idx++)
                    {
                        bool hasCounts = (0 != prereleaseCharCounts[idx]) || (0 != metaCharCounts[idx]);

                        if (hasCounts)
                        {
                            Console.WriteLine(
                                $"{idx},x,x,x,{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
                        }
                    }

                    summary.Write();
                    summary.Write(new StreamWriter("SummaryData.txt", false, Encoding.ASCII));

                    Console.ReadKey();
                    return 0;
                }
            }
        }

        static uint CountDots(ref string str)
        {
            // There's probably a better .Net way to do this.
            uint count = 0;
            foreach(var c in str)
            {
                if (c == _dot) count++;
            }

            return count;
        }

        static void SetTagFieldCounts(
            ref string tag, 
            ref ulong hasTagCountInOut, 
            uint maxLength, 
            ulong count, 
            ref ulong[] countInOut, 
            ref ulong excessCountInOut, 
            ulong maxFields, 
            ref ulong[] fieldCountsInOut, 
            ref ulong excessFieldsCountInOut)
        {
            hasTagCountInOut++;

            SetCharFieldCounts(tag.Length, maxLength, count, ref countInOut[tag.Length], ref excessCountInOut);
            
            ulong fieldCount = CountDots(ref tag);

            if (fieldCount < maxFields)
            {
                fieldCountsInOut[fieldCount] += count > 0 ? count : 1;
            }
            else
            {
                excessFieldsCountInOut++;
            }
        }

        static void SetCharFieldCounts(int length, uint maxLength, ulong count, ref ulong countInOut, ref ulong excessCountInOut)
        {
            if (length < maxLength)
            {
                countInOut += count > 0 ? count : 1;
            }
            else
            {
                excessCountInOut++;
            }
        }
    }
}
