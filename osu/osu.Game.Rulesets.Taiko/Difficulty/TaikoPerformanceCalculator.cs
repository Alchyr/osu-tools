// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    public class TaikoPerformanceCalculator : PerformanceCalculator
    {
        protected new TaikoDifficultyAttributes Attributes => (TaikoDifficultyAttributes)base.Attributes;

        private Mod[] mods;
        private int countGreat;
        private int countGood;
        private int countMeh;
        private int countMiss;

        public TaikoPerformanceCalculator(Ruleset ruleset, WorkingBeatmap beatmap, ScoreInfo score)
            : base(ruleset, beatmap, score)
        {
        }

        public override double Calculate(Dictionary<string, double> categoryDifficulty = null)
        {
            mods = Score.Mods;
            countGreat = Convert.ToInt32(Score.Statistics[HitResult.Great]);
            countGood = Convert.ToInt32(Score.Statistics[HitResult.Good]);
            countMeh = Convert.ToInt32(Score.Statistics[HitResult.Meh]);
            countMiss = Convert.ToInt32(Score.Statistics[HitResult.Miss]);

            // Don't count scores made with supposedly unranked mods
            if (mods.Any(m => !m.Ranked))
                return 0;

            // Custom multipliers for NoFail and SpunOut.
            double multiplier = 1.1; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is ModNoFail))
                multiplier *= 0.90;

            if (mods.Any(m => m is ModEasy))
                multiplier *= 0.90;

            if (mods.Any(m => m is ModFlashlight<TaikoHitObject>))
                // Apply length bonus simply because it becomes a lot harder on longer maps.
                multiplier *= Math.Min((1.02 * Math.Pow(Attributes.WeightedObjects / 15000.0, 1.5) + 1.05), 1.125);

            if (mods.Any(m => m is ModHidden))
                multiplier *= 1.10;

            if (mods.Any(m => m is ModHardRock))
                multiplier *= 1.05;

            double strainValue = computeStrainValue();
            double accuracyValue = computeAccuracyValue();
            double totalValue =
                Math.Pow(
                    Math.Pow(strainValue, 1.2) +
                    Math.Pow(accuracyValue, 1.2), 1.0 / 1.2
                ) * multiplier;

            if (categoryDifficulty != null)
            {
                categoryDifficulty["Strain"] = strainValue;
                categoryDifficulty["Accuracy"] = accuracyValue;
            }

            return totalValue;
        }

        private double computeStrainValue()
        {
            double strainValue = Math.Pow(Attributes.StarRating, 1.85) * 6.5 + 0.1; //0.1 gives a small minimum

            strainValue *= Math.Min(1.25, Math.Pow(Attributes.WeightedObjects / 1000.0, 0.2));

            strainValue *= Math.Pow(Score.Accuracy, 2);

            return strainValue;
        }

        private double computeAccuracyValue()
        {
            if (Attributes.GreatHitWindow <= 0)
            {
                return 0;
            }

            // Values are based on experimentation.
            double accValue = (300.0 / (Attributes.GreatHitWindow + 21.0)) * 2; // Value is based on hitwindow
            accValue *= Math.Pow(Score.Accuracy, 14); // Scale with accuracy
            accValue *= Math.Pow(Attributes.StarRating, 1.5); // Scale with difficulty, slightly exponentially

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            return accValue * Math.Min(1.15, Math.Pow(Attributes.WeightedObjects / 1000.0, 0.2));
        }

        private int totalHits => countGreat + countGood + countMeh + countMiss;
    }
}
