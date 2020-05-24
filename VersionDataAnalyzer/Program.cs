// Copyright (c) Joseph W Donahue
//
// Licensed under the terms of the MIT license (https://opensource.org/licenses/MIT). See LICENSE.TXT.
//
//  Contact: coders@sharperhacks.com
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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

    // Usage:
    //  dotnet run [<inputfile> [...]] | [-i<inputDirectory>] [-o<outputDirectory>] [-<WaitForInputOnExit>]
    //
    //      inputFile               Either at least one input file, or a -i are required.
    //      -i-i<inputDirectory>    Optional. If provided, all files in inputDirectory will be processed.
    //      -o<outputDirectory>     Optional. If provided, all output files are written to this directory.
    //                              Default: Current directory.
    //
    //  Options are not case sensitive.
    //
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
        private const uint _tagCharCountSlots = 100;
        private const uint _tagFieldCountSlots = _tagCharCountSlots;

        private const char _dot = '.';

        // This regex borrowed from https://github.com/semver/semver/blob/master/semver.md#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
        private const string _semverRegexStr = @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";
        private const string _countAndSemverRegexStr = @"(?<Count>\d*),((?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?)$";

#if false // TODO:
        private const string _countAndNumericRegexStr = @"(?<Count>\d*),?<version>[\d\.]*$";
        private const string _countNumericAlphaRegexStr = @"(?<Count>\d*),?<version>[\d\.a-zA-Z-]*$";
        private const string _numericRegexStr = @"?<version>[\d\.]*$";
        private const string _numericAlphaRegexStr = @"?<version>[\d\.a-zA-Z-]*$";
#endif

        private static Regex _semverRegex = new Regex(_semverRegexStr, RegexOptions.Compiled);

        private static Regex _countAndSemverRegex = new Regex(_countAndSemverRegexStr, RegexOptions.Compiled);

        private static DirectoryInfo _inputDirectory;
        private static DirectoryInfo _outputDirectory;
        private static DirectoryInfo _summaryDirectory;

        private static List<FileInfo> _inputFiles = new List<FileInfo>();
        private static List<FileInfo> _cleanedNearMisses = new List<FileInfo>();

        private static bool _waitForInputOnExit = false;

        static int Main(string[] args)
        {
            if (!ArgsHandled(args)) return -1;

            foreach (var inputFileInfo in _inputFiles)
            {
                ProcessInputFile(inputFileInfo);
            }

            foreach (var inputFileInfo in _cleanedNearMisses)
            {
                ProcessInputFile(inputFileInfo);
            }

            Console.WriteLine("Done.");

            if (_waitForInputOnExit)
            {
                Console.WriteLine("Any key to continue.");
                Console.ReadKey();
            }

            return 0;
        }

        static bool ArgsHandled(string[] args)
        {
            foreach (var arg in args)
            {
                if (!ProcessArg(arg)) return false;
            }

            SetDefaultsIfNeeded();

//            QueueInputFiles();

            if (0 == _inputFiles.Count)
            {
                Console.Error.WriteLine("ERROR: No input files specified.");
                return false;
            }

            return true;
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

        static string CreateOutputPathFileName(FileInfo inputFileInfo, string name, string extension = null)
        {
            if (null == extension) extension = "txt";
            string datePrefix = inputFileInfo.CreationTimeUtc.ToString("yyyy-MM-dd-HH-mm-ss");
            string outFileName = $"{datePrefix}_{inputFileInfo.Name}_{name}.{extension}";
            return Path.Combine(_outputDirectory.FullName, outFileName);
        }

        static string GetBadLinesPathFileName(FileInfo inputFileInfo)
        {
            return Path.Combine(_outputDirectory.FullName, CreateOutputPathFileName(inputFileInfo, "BadLines"));
        }

        static string GetCleanedNearMissesFileName(FileInfo inputFileInfo)
        {
            return Path.Combine(_outputDirectory.FullName, CreateOutputPathFileName(inputFileInfo, "CleanedNearMisses"));
        }

        static string GetOutputPathFileName(FileInfo inputFileInfo)
        {
            return Path.Combine(_outputDirectory.FullName, CreateOutputPathFileName(inputFileInfo, "Counts", "csv"));
        }

        static string GetSummarryOutputPathFileName(FileInfo inputFileInfo)
        {
            return Path.Combine(_outputDirectory.FullName, CreateOutputPathFileName(inputFileInfo, "Summary"));
        }

        static bool ProcessArg(string arg)
        {
            if (arg.ToLower().StartsWith("-o"))
            {
                _outputDirectory = new DirectoryInfo(arg.Substring(2));

                if (!_outputDirectory.Exists)
                {
                    Directory.CreateDirectory(_outputDirectory.FullName);
                }
            }
            else if (arg.ToLower().StartsWith("-i"))
            {
                _inputDirectory = new DirectoryInfo(arg.Substring(2));

                if (_inputDirectory.Exists)
                {
                    foreach (var file in _inputDirectory.EnumerateFiles())
                    {
                        _inputFiles.Add(file);
                    }
                }
                else
                {
                    Console.Error.WriteLine($"ERROR: Input directory does not exist: {_inputDirectory.FullName}");
                    return false;
                }
            }
            else if (arg.ToLower() == "-waitforinputonexit")
            {
                _waitForInputOnExit = true;
            }
            else
            {
                // It had better be a file name.
                FileInfo fileInfo = new FileInfo(arg);

                if (!fileInfo.Exists)
                {
                    Console.Error.WriteLine($"ERROR: Input file does not exist: {fileInfo.FullName}");
                    return false;
                }

                _inputFiles.Add(fileInfo);
            }

            return true;
        }

        static void ProcessInputFile(FileInfo inputFileInfo)
        {
            // TODO: Counts class.
            var majorCounts = new ulong[_tripleCharCountSlots];
            var minorCounts = new ulong[_tripleCharCountSlots];
            var patchCounts = new ulong[_tripleCharCountSlots];
            var prereleaseCharCounts = new ulong[_tagCharCountSlots];
            var prereleaseFieldCounts = new ulong[_tagFieldCountSlots];
            var metaCharCounts = new ulong[_tagCharCountSlots];
            var metaFieldCounts = new ulong[_tagFieldCountSlots];

            ulong hasPrereleaseCount = 0;
            ulong hasMetaCount = 0;

            SummaryData summary;

            string badLinesPathFileName = GetBadLinesPathFileName(inputFileInfo);
            string cleanedNearMissPathFileName = GetCleanedNearMissesFileName(inputFileInfo);
            string outputPathFileName = GetOutputPathFileName(inputFileInfo);

            using (StreamReader inputFile = new StreamReader(File.OpenRead(inputFileInfo.FullName)))
            {
                using (StreamWriter badLinesFile = new StreamWriter(badLinesPathFileName, false, Encoding.ASCII))
                {
                    using (StreamWriter nearMisFile = new StreamWriter(cleanedNearMissPathFileName, false, Encoding.ASCII))
                    {
                        summary = new SummaryData(inputFileInfo, 
                            badLinesPathFileName, 
                            cleanedNearMissPathFileName,
                            outputPathFileName);

                        while (!inputFile.EndOfStream)
                        {
                            string line = inputFile.ReadLine();

                            summary.LineCount++;

                            Match match = _countAndSemverRegex.Match(line);

                            ulong count = 0;

                            // We accept two kinds of records:
                            //   count,semver
                            //   semver
                            if (match.Success)
                            {
                                count = ulong.Parse(match.Groups["Count"].Value);
                                summary.AddSemVerLength(line.Length - match.Groups["Count"].Value.Length - 1);
                            }
                            else
                            {
                                match = _semverRegex.Match(line);
                                summary.AddSemVerLength(line.Length);
                            }

                            if (!match.Success)
                            {
                                summary.NotSemverCount++;

                                char firstChar = line[0];
                                if (('v' == firstChar) || ('V' == firstChar))
                                {
                                    string prefixStripped = line.Substring(1);
                                    match = _semverRegex.Match(prefixStripped);
                                    if (match.Success)
                                    {
                                        summary.NearMissCount++;
                                        nearMisFile.WriteLine(prefixStripped);
                                    }
                                }
                                else
                                {
                                    badLinesFile.WriteLine(line);
                                    
                                    // TODO: Classify string types by numeric or alphanumeric.
                                }

                                // Best we can do here.
                                summary.AddSemVerLength(line.Length);
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

                            SetCharFieldCounts(majorLength, _tripleCharCountSlots, count,
                                ref majorCounts[majorLength],
                                ref majorLengthExcessiveCount);
                            SetCharFieldCounts(minorLength, _tripleCharCountSlots, count,
                                ref minorCounts[minorLength],
                                ref minorLengthExcessiveCount);
                            SetCharFieldCounts(patchLength, _tripleCharCountSlots, count,
                                ref patchCounts[patchLength],
                                ref patchLengthExcessiveCount);

                            string prerelease = match.Groups["prerelease"].Value;
                            if (!string.IsNullOrEmpty(prerelease))
                            {
                                SetTagFieldCounts(
                                    ref prerelease,
                                    ref hasPrereleaseCount,
                                    _tagCharCountSlots,
                                    count,
                                    ref prereleaseCharCounts,
                                    ref prereleaseLengthExcessiveCount,
                                    _tagFieldCountSlots,
                                    ref prereleaseFieldCounts,
                                    ref prereleaseFieldsExcessiveCount);
                            }

                            string meta = match.Groups["buildmetadata"].Value;
                            if (!string.IsNullOrEmpty(meta))
                            {
                                SetTagFieldCounts(
                                    ref meta,
                                    ref hasMetaCount,
                                    _tagCharCountSlots,
                                    count,
                                    ref metaCharCounts,
                                    ref metaLengthExcessiveCount,
                                    _tagFieldCountSlots,
                                    ref metaFieldCounts,
                                    ref metaFieldsExcessiveCount
                                );
                            }
                        }
                    }

                    using (StreamWriter outputFile = new StreamWriter(outputPathFileName, false, Encoding.ASCII))
                    {
                        outputFile.WriteLine("Char Count,Major,Minor,Patch,Prerelease,PrereleaseFields,Meta,MetaFields");
                        for (uint idx = 0; idx < _tripleCharCountSlots; idx++)
                        {
                            bool hasCounts = (0 != majorCounts[idx]) ||
                                             (0 != minorCounts[idx]) ||
                                             (0 != patchCounts[idx]) ||
                                             (0 != prereleaseCharCounts[idx]) ||
                                             (0 != metaCharCounts[idx]);

                            if (hasCounts || (0 == idx))
                            {
                                outputFile.WriteLine(
                                    $"{idx},{majorCounts[idx]},{minorCounts[idx]},{patchCounts[idx]},{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
                            }
                        }

                        for (uint idx = _tripleCharCountSlots; idx < _tagCharCountSlots; idx++)
                        {
                            bool hasCounts = (0 != prereleaseCharCounts[idx]) || (0 != metaCharCounts[idx]);

                            if (hasCounts || (0 == idx))
                            {
                                outputFile.WriteLine(
                                    $"{idx},x,x,x,{prereleaseCharCounts[idx]},{prereleaseFieldCounts[idx]},{metaCharCounts[idx]},{metaFieldCounts[idx]}");
                            }
                        }
                    }

                    summary.Write();

                    using (var summaryOut = new StreamWriter(GetSummarryOutputPathFileName(inputFileInfo), false, Encoding.ASCII))
                    {
                        summary.Write(summaryOut);
                    }

                    if (summary.NearMissCount > 0)
                    {
                        _cleanedNearMisses.Add(new FileInfo(cleanedNearMissPathFileName));
                    }
                }
            }
        }

        static void SetDefaultsIfNeeded()
        {
            if ((null == _outputDirectory) || (null == _summaryDirectory))
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                if (null == _outputDirectory)
                {
                    _outputDirectory = new DirectoryInfo(currentDirectory);
                }

                if (null == _summaryDirectory)
                {
                    _summaryDirectory = _outputDirectory;
                }
            }
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
