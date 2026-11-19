// Source - https://stackoverflow.com/a/11930875
// Posted by necrogt4, modified by community. See post 'Timeline' for change history
// Retrieved 2026-11-19, License - CC BY-SA 4.0

namespace Fuzzers.Collections;

public static class IEnumerableExtensions
{

    extension<T>(IEnumerable<T> sequence)
    {
        public T RandomElementByWeight(Func<T, float> weightSelector, Random? rand = null)
        {
            rand ??= Random.Shared;

            float totalWeight = sequence.Sum(weightSelector);
            // The weight we are after...
            float itemWeightIndex = (float)rand.NextDouble() * totalWeight;
            float currentWeightIndex = 0;

            foreach (var item in from weightedItem in sequence select new { Value = weightedItem, Weight = weightSelector(weightedItem) })
            {
                currentWeightIndex += item.Weight;

                // If we've hit or passed the weight we are after for this item then it's the one we want....
                if (currentWeightIndex > itemWeightIndex)
                    return item.Value;
            }

            return default!;
        }
    }
}
