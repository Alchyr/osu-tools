﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-tools/master/LICENCE

using System.Collections.Generic;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;

namespace PerformanceCalculator.Performance
{
    public class PerformanceProcessor : Processor
    {
        private readonly PerformanceCommand command;

        public PerformanceProcessor(PerformanceCommand command)
        {
            this.command = command;
        }

        private WorkingBeatmap workingBeatmap;
        private Ruleset ruleset;

        protected override void Execute(BeatmapManager beatmaps, ScoreStore scores)
        {
            if (workingBeatmap == null)
            {
                try
                {
                    // Try import as .osz archive
                    using (var stream = File.OpenRead(command.Beatmap))
                        beatmaps.Import(new ZipArchiveReader(stream, command.Beatmap));
                }
                catch
                {
                    // Import as .osu
                    beatmaps.Import(new SingleFileArchiveReader(command.Beatmap));
                }
            }

            foreach (var f in command.Replays)
            {
                var score = scores.ReadReplayFile(f);

                if (ruleset == null)
                    ruleset = score.Ruleset.CreateInstance();

                // Create beatmap
                if (workingBeatmap == null)
                    workingBeatmap = beatmaps.GetWorkingBeatmap(score.Beatmap);
                workingBeatmap.Mods.Value = score.Mods;

                // Convert + process beatmap
                IBeatmap converted = workingBeatmap.GetPlayableBeatmap(score.Ruleset);

                var categoryAttribs = new Dictionary<string, double>();
                double pp = ruleset.CreatePerformanceCalculator(converted, score).Calculate(categoryAttribs);
                
                command.Console.WriteLine(f);
                command.Console.WriteLine($"{"Player".PadRight(15)}: {score.User.Username}");
                command.Console.WriteLine($"{"Mods".PadRight(15)}: {score.Mods.Select(m => m.ShortenedName).Aggregate((c, n) => $"{c}, {n}")}");
                foreach (var kvp in categoryAttribs)
                    command.Console.WriteLine($"{kvp.Key.PadRight(15)}: {kvp.Value}");
                command.Console.WriteLine($"{"pp".PadRight(15)}: {pp}");
            }
        }
    }
}
