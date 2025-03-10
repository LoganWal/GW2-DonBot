namespace DonBot.Extensions
{
    public static class ListExtensions
    {
        public static bool CheckIndexIsValid(this List<List<double>> list, int dimension1, int dimension2)
        {
            return dimension1 < list.Count && dimension2 < list[dimension1].Count;
        }

        public static bool CheckIndexIsValid(this List<List<long>> list, int dimension1, int dimension2)
        {
            return dimension1 < list.Count && dimension2 < list[dimension1].Count;
        }
    }
}