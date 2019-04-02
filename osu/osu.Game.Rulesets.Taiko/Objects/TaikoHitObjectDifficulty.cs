// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Taiko.Objects
{
    internal class TaikoHitObjectDifficulty
    {
        /// <summary>
        /// Factor by how much individual / overall strain decays per second.
        /// </summary>
        /// <remarks>
        /// These values are results of tweaking a lot and taking into account general feedback.
        /// </remarks>
        internal const double DECAY_BASE = 0.30;

        private const double base_speed_value = 0.5; //the default addition value

        private const double color_speed_value = 1.0; //the addition value for don/kat speed
        private const double color_decay_base = 0.25;
        private const double color_decay_scale = 500;
        private const double mono_weight = 0.3;

        //type
        private const double type_addition_decay = 0.8; //The rate at which typeswap bonus decays
        private const double type_final_scale = 1.5; // Scales the final addition value.

        private const double base_type_bonus = 1.6; // The base bonus value for type change
        private const double type_bonus_scale = 2.3; // Determines how bonus scales with number of objects of same type
        private const double type_swap_adjust = 0.8; //Addition to denominator of bonus - affects how it scales
        private const double type_bonus_cap = 1.5; // Determines final maximum bonus when swapping

        private const double same_typeswitch_loss = 0.5; // The loss in bonus from going from repeating even -> even or odd -> odd
        private const double even_typeswitch_loss = 0.75; // The loss in bonus from being even
        private const double close_repeat_loss = 0.65; // The loss in bonus from repeating the same length of object twice in a row (per color)
        private const double late_repeat_loss = 0.75; // The loss in bonus from repeating the same length of object with a gap between (per color)

        //rhythm
        private const double rhythm_addition_decay = 0.85; // The rate at which rhythm bonus decays

        private const double tiny_speedup_bonus = 0.35; // Very small speed increases
        private const double small_speedup_bonus = 1.1; // This mostly affects 1/4 -> 1/6 and other weird rhythms.
        private const double moderate_speedup_bonus = 0.3; // Speed doubling
        private const double large_speedup_bonus = 0.45; // Anything that more than doubles speed. Affects doubles.

        private const double tiny_speeddown_bonus = 0.2; // Very small speed decrease
        private const double small_speeddown_bonus = 0.45; // This mostly affects 1/6 -> 1/4, and other weird rhythms.
        private const double large_speeddown_bonus = 0.2; // Half speed; for slowdown, no need for more specific.


        internal TaikoHitObject BaseHitObject;

        /// <summary>
        /// Measures note density in a way
        /// </summary>
        internal double Strain = 0;

        /// <summary>
        /// Speed regardless of color
        /// </summary>
        internal double baseSpeed = 0;

        /// <summary>
        /// Speed based on don density
        /// </summary>
        internal double donSpeed = 0;
        internal double lastDonPos = 0;

        /// <summary>
        /// Speed based on kat density
        /// </summary>
        internal double katSpeed = 0;
        internal double lastKatPos = 0;

        internal double typeAddition = 0;
        internal double rhythmAddition = 0;


        /// <summary>
        /// Tracks pattern repetition
        /// </summary>
        internal int[][] previousLengths = null;
        
        /// <summary>
        /// To invalidate nearby objects
        /// </summary>
        internal TaikoHitObjectDifficulty previousHitObject;
        internal TaikoHitObjectDifficulty nextHitObject;


        internal bool isValid = true;


        private double timeElapsed;
        private int sameTypeSince = 1;

        private bool isRim => BaseHitObject is RimHit;

        public TaikoHitObjectDifficulty(TaikoHitObject baseHitObject)
        {
            BaseHitObject = baseHitObject;
        }

        internal void CalculateStrains(TaikoHitObjectDifficulty previousHitObject, double timeRate)
        {
            isValid = true;

            this.previousHitObject = previousHitObject;

            // Form a linkedlist for ease of invalidating objects during calculation
            this.previousHitObject.nextHitObject = this;
            this.previousHitObject = previousHitObject;

            timeElapsed = (BaseHitObject.StartTime - previousHitObject.BaseHitObject.StartTime) / timeRate;


            //Decay
            double decay = Math.Max(0, Math.Pow(DECAY_BASE, timeElapsed / 1000) - 0.01);
            if (timeElapsed > 1000) // Objects more than 1 second apart gain somewhat exponentially less strain.
            {
                decay /= 1 + ((timeElapsed - 1000) / 100);
            }

            //Scaling as speed increases
            double baseAdditionScale = 1;
            double rhythmAdditionScale = 0.3 + (0.7 * timeElapsed / 65.0);
            if (timeElapsed > 65)
            {
                rhythmAdditionScale = 1;
            }
            else
            {
                baseAdditionScale *= Math.Pow(65.0 / timeElapsed, 1 / 5);
            }


            // Only if we are no slider or spinner we get an extra addition
            if (previousHitObject.BaseHitObject is Hit && BaseHitObject is Hit)
            {
                // To remove value of sliders/spinners, set default addition to 0 along with type and rhythm additions, and increase to 1 here
                typeAddition = (previousHitObject.typeAddition + (typeChangeAddition() * type_final_scale)) * decay * type_addition_decay;
                rhythmAddition = (previousHitObject.rhythmAddition + (rhythmChangeAddition() * rhythmAdditionScale)) * decay * rhythm_addition_decay;
            }
            else
            {
                typeAddition = previousHitObject.typeAddition * decay;
                rhythmAddition = previousHitObject.rhythmAddition * decay;
            }


            // Speed
            baseSpeed = (previousHitObject.baseSpeed + (base_speed_value * baseAdditionScale)) * decay;

            double finalSpeed = (baseSpeed + (colorSpeedValue(timeRate, baseAdditionScale) * mono_weight)) / (1 + mono_weight);


            Strain = finalSpeed + Math.Pow(Math.Pow(typeAddition, 1.5) + Math.Pow(rhythmAddition, 1.5), 2/3);
        }
        
        private double typeChangeAddition()
        {
            previousLengths = previousHitObject.previousLengths;

            // This occurs when the previous object is a slider or spinner, or on the first object. Since key doesn't matter for those, count being reset is fine.
            if (previousLengths == null)
            {
                previousLengths = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 } };
            }

            // If we don't have the same hit type, trigger a type change!
            if (previousHitObject.isRim ^ isRim) // for bool xor is equivalent to != so either could be used
            {
                double typeBonus = base_type_bonus - (type_bonus_scale / (previousHitObject.sameTypeSince + type_swap_adjust));
                double multiplier = 1.0;


                if (previousHitObject.isRim) // Previous is kat
                {
                    if (previousHitObject.sameTypeSince % 2 == previousLengths[0][0] % 2) //previous don length was same even/odd
                        multiplier *= same_typeswitch_loss;

                    if (previousHitObject.sameTypeSince % 2 == 0)
                        multiplier *= even_typeswitch_loss;

                    if (previousLengths[1][0] == previousHitObject.sameTypeSince)
                        multiplier *= close_repeat_loss;

                    if (previousLengths[1][1] == previousHitObject.sameTypeSince)
                        multiplier *= late_repeat_loss;

                    previousLengths[1][1] = previousLengths[1][0];
                    previousLengths[1][0] = previousHitObject.sameTypeSince;
                }
                else // Don
                {
                    if (previousHitObject.sameTypeSince % 2 == previousLengths[1][0] % 2) //previous kat length was same even/odd
                        multiplier *= same_typeswitch_loss;

                    if (previousHitObject.sameTypeSince % 2 == 0)
                        multiplier *= even_typeswitch_loss;

                    if (previousLengths[0][0] == previousHitObject.sameTypeSince)
                        multiplier *= close_repeat_loss;

                    if (previousLengths[0][1] == previousHitObject.sameTypeSince)
                        multiplier *= late_repeat_loss;

                    previousLengths[0][1] = previousLengths[0][0];
                    previousLengths[0][0] = previousHitObject.sameTypeSince;
                }

                return Math.Min(type_bonus_cap, typeBonus * multiplier);
            }
            // No type change? Increment counter
            else
            {
                sameTypeSince = previousHitObject.sameTypeSince + 1;
                return 0;
            }
        }

        private double rhythmChangeAddition()
        {
            // We don't want a division by zero if some random mapper decides to put 2 HitObjects at the same time.
            if (previousHitObject.timeElapsed == 0)
                return 0;

            double change = (timeElapsed / previousHitObject.timeElapsed);

            if (change < 0.48) // Speedup by more than 2x
                return large_speedup_bonus;
            else if (change <= .51) // Speedup by 2x
                return moderate_speedup_bonus;
            else if (change <= 0.9) // Speedup between small amount and 2x
                return small_speedup_bonus;
            else if (change < .95) // Speedup a very small amount
                return tiny_speedup_bonus;
            else if (change > 1.95) // Slowdown by half speed or more
                return large_speeddown_bonus;
            else if (change > 1.15) //Slowdown less than half speed
                return small_speeddown_bonus;
            else if (change > 1.02) //Slowdown a very small amount
                return tiny_speeddown_bonus;

            return 0;
        }
        
        private double colorSpeedValue(double timeRate, double baseAdditionScale)
        {
            if (isRim)
            {
                donSpeed = previousHitObject.donSpeed;
                lastDonPos = previousHitObject.lastDonPos;

                double katElapsed = (BaseHitObject.StartTime - previousHitObject.lastKatPos) / timeRate;
                double decay = Math.Max(0, Math.Pow(color_decay_base, katElapsed / color_decay_scale) - 0.01);

                katSpeed = (previousHitObject.katSpeed + (color_speed_value * baseAdditionScale)) * decay;

                lastKatPos = BaseHitObject.StartTime;

                return katSpeed;
            }
            else
            {
                katSpeed = previousHitObject.katSpeed;
                lastKatPos = previousHitObject.lastKatPos;

                double donElapsed = (BaseHitObject.StartTime - previousHitObject.lastDonPos) / timeRate;
                double decay = Math.Max(0, Math.Pow(color_decay_base, donElapsed / color_decay_scale) - 0.01);

                donSpeed = (previousHitObject.donSpeed + (color_speed_value * baseAdditionScale)) * decay;

                lastDonPos = BaseHitObject.StartTime;

                return donSpeed;
            }
        }

        internal void InvalidateNear(double actualStrainStep)
        {
            InvalidatePrevious(actualStrainStep);
            InvalidateNext(actualStrainStep);
        }
        private void InvalidatePrevious(double actualStrainStep)
        {
            if (previousHitObject != null)
            {
                if (BaseHitObject.StartTime - previousHitObject.BaseHitObject.StartTime < actualStrainStep)
                {
                    previousHitObject.isValid = false;
                    previousHitObject.InvalidatePrevious(actualStrainStep - (BaseHitObject.StartTime - previousHitObject.BaseHitObject.StartTime));
                }
            }
        }
        private void InvalidateNext(double actualStrainStep)
        {
            if (nextHitObject != null)
            {
                if (nextHitObject.BaseHitObject.StartTime - BaseHitObject.StartTime < actualStrainStep)
                {
                    nextHitObject.isValid = false;
                    nextHitObject.InvalidateNext(actualStrainStep - (nextHitObject.BaseHitObject.StartTime - BaseHitObject.StartTime));
                }
            }
        }

        private enum TypeSwitch
        {
            None,
            Even,
            Odd
        }
    }
}
