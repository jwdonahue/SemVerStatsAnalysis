// Copyright (c) Joseph W Donahue
//
// Licensed under the terms of the MIT license (https://opensource.org/licenses/MIT). See LICENSE.TXT.
//
//  Contact: coders@sharperhacks.com
// ---------------------------------------------------------------------------

using System;
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
        private const char _dot = '.';
        private const char _comma = ',';
        private const char _plus = '+';
        private const char _hyphen = '-';
        private const string _doubleCommas = ",,";

        // This regex borrowed from https://github.com/semver/semver/blob/master/semver.md#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
        private const string _semverRegexStr = @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";
        private const string _countAndSemverRegexStr = @"(?<Count>\d*),((?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?)$";

        private static Regex _semverRegex = new Regex(_semverRegexStr, RegexOptions.Compiled);

        private static Regex _countAndSemverRegex = new Regex(_countAndSemverRegexStr, RegexOptions.Compiled);

        static int Main(string[] args)
        {
            UInt64 semverCount = 0;
            UInt64 notSemverCount = 0;

            var majorCounts = new UInt64[100];
            var minorCounts = new UInt64[100];
            var patchCounts = new UInt64[100];
            var prereleaseCharCounts = new UInt64[256];
            var prereleaseFieldCounts = new UInt64[256];
            var metaCharCounts = new UInt64[256];
            var metaFieldCounts = new UInt64[256];

            UInt64 hasPrereleaseCount = 0;
            UInt64 hasMetaCount = 0;

            if (args.Length != 1)
            {
                Console.WriteLine("Usage: VersionDataAnalyzer input_file\n");
                return -1;
            }

            string[] lines = System.IO.File.ReadAllLines(args[0]);

            for (long idx = 0; idx < lines.LongLength; idx++)
            {
                Match match = _countAndSemverRegex.Match(lines[idx]);

                UInt64 count = 0;

                if (match.Success)
                {
                    count = UInt64.Parse(match.Groups["Count"].Value);
                }
                else
                {
                    match =_semverRegex.Match(lines[idx]);
                }

                if (!match.Success)
                {
                    notSemverCount++;

                    // TODO: Write this to an error log instead and add an ignore non-SemVer switch.
                    //Console.WriteLine($"ERROR: Failed to match line #{idx + 1}: {lines[idx]}\nContinuing to process file...");
#if false
                    if ('v' == countAndSemverMatch.Groups["Semver"].Value.ToLower()[0])
                    {
                        semverMatch = _semverRegex.Match(countAndSemverMatch.Groups["Semver"].Value.Substring(1));
                        if (semverMatch.Success)
                        {
                            Console.WriteLine($"WARNING: Version string counted as non-SemVer would have passed without the leading {countAndSemverMatch.Groups["Semver"].Value[0]}");
                        }
                    }
#endif
                    continue;
                }

                semverCount++;

                majorCounts[match.Groups["major"].Value.Length] += count > 0 ? count : 1;
                minorCounts[match.Groups["minor"].Value.Length] += count > 0 ? count : 1;
                patchCounts[match.Groups["patch"].Value.Length] += count > 0 ? count : 1;

                string prerelease = match.Groups["prerelease"].Value;
                if (!string.IsNullOrEmpty(prerelease))
                {
                    hasPrereleaseCount++;
                    prereleaseCharCounts[prerelease.Length]++;
                    prereleaseFieldCounts[CountDots(ref prerelease)]++;
                }

                string meta = match.Groups["buildmetadata"].Value;
                if (!string.IsNullOrEmpty(meta))
                {
                    hasMetaCount++;
                    metaCharCounts[meta.Length]++;
                    metaFieldCounts[CountDots(ref meta)]++;
                }
            }

            Console.WriteLine("Char Count,Major,Minor,Patch,Prerelease,PrereleaseFields,Meta,MetaFields");
            for (uint idx = 0; idx < 100; idx++)
            {
                Console.WriteLine($"{idx},{majorCounts[idx]},{minorCounts[idx]},{patchCounts[idx]},{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
            }

            for (uint idx = 100; idx < 256; idx++)
            {
                Console.WriteLine($"{idx},x,x,x,{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
            }

            Console.WriteLine($"\n\nSemVer Count:,{semverCount}");
            Console.WriteLine($"Non-SemVer Count:,{notSemverCount}\n");

            Console.ReadKey();
            return 0;
        }

        static uint CountDots(ref string str)
        {
            uint count = 0;
            foreach(var c in str)
            {
                if (c == '.') count++;
            }

            return count;
        }

    }
}
