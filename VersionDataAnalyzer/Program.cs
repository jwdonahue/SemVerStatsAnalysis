// Copyright (c) Joseph W Donahue
//
// Licensed under the terms of the MIT license (https://opensource.org/licenses/MIT). See LICENSE.TXT.
//
//  Contact: coders@sharperhacks.com
// ---------------------------------------------------------------------------

using System;

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
    // Note that if meta is populated, there must be a prerelease field,
    // even if it is empty.
    //
    // This program came about as an expedience, after hand messaging the first
    // data set and finding OpenOffice Calc to be less than desired.
    //
    // I can/will make this better.

    // TODO: Add output file argument.
    // TODO: Add support to read records of the form 'count,semver'.
    // TODO: Add support to read a list of raw semver strings.

    class Program
    {
        static int Main(string[] args)
        {
            uint versionCount = 0;
            var majorCounts = new uint[100];
            var minorCounts = new uint[100];
            var patchCounts = new uint[100];
            var prereleaseCounts = new uint[100];
            var prereleaseFieldCounts = new uint[100];
            var metaCounts = new uint[100];
            var metaFieldCounts = new uint[100];

            if (args.Length != 1)
            {
                Console.WriteLine("Usage: VersionDataAnalyzer input_file\n");
                return -1;
            }

            string[] lines = System.IO.File.ReadAllLines(args[0]);

            foreach (var line in lines)
            {
                string[] fields = AddMissingFields(line).Split(',');
                uint count;

                if (!uint.TryParse(fields[0], out count)) continue; // First line can be header.

                versionCount += count;
                majorCounts[fields[1].Length] += count;
                minorCounts[fields[2].Length] += count;
                patchCounts[fields[3].Length] += count;
                
                prereleaseCounts[fields[4].Length] += count;
                if (fields[4].Length > 0)
                {
                    prereleaseFieldCounts[CountDots(fields[4])] += count;
                }
                else
                {
                    prereleaseFieldCounts[0] += count;
                }

                metaCounts[fields[5].Length] += count;
                if (fields[5].Length > 0)
                {
                    metaFieldCounts[CountDots(fields[5])] += count;
                }
                else
                {
                    metaFieldCounts[0] += count;
                }
            }

            Console.WriteLine("Char Count,Major,Minor,Patch,Prerelease,PrereleaseFields,Meta,MetaFields");
            for (uint idx = 0; idx < 100; idx++)
            {
                Console.WriteLine($"{idx},{majorCounts[idx]},{minorCounts[idx]},{patchCounts[idx]},{prereleaseCounts[idx]},{prereleaseFieldCounts[idx]},{metaCounts[idx]},{metaFieldCounts[idx]}");
            }

            Console.WriteLine($"\n\nversionCount:,{versionCount}");

            Console.ReadKey();
            return 0;
        }

        static uint CountDots(string str)
        {
            uint count = 0;
            foreach(var c in str)
            {
                if (c == '.') count++;
            }

            return count;
        }

        static string AddMissingFields(string str)
        {
            int count = 0;
            foreach (var c in str)
            {
                if (c == ',') count++;
            }


            for (int commasNeeded = (5 - count); commasNeeded > 0; commasNeeded--)
            {
                str += ',';
            }

            return str;
        }
    }
}
