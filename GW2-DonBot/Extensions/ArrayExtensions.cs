namespace Extensions
{
    public static class ArrayExtensions
    {
        public static bool CheckIndexIsValid(this double[][] array, int dimension1, int dimension2)
        {
            return dimension1 < array.GetLength(0) && dimension2 < array[dimension1].GetLength(0);
        }
    }
}