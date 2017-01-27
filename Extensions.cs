namespace ConsoleWebDownload
{
    public static class Extensions
    {
        public static int FirstIndexBackwards(this string sourse, char item, int startPosition)
        {
            int length = -1;
            int current = startPosition;
            while (sourse[current] != item)
            {
                length = length + 1;
                current = current - 1;
            }

            return length;
        }
    }
}
