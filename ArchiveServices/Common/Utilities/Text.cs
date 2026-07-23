using System;

namespace ArchiveServices.Common.Utilities
{
    public static class Text
    {
        public static System.Collections.Generic.List<string> WordWrap(this string text, int maxCharactersPerLine)
        {
            text = text.Trim();
            var result = new System.Collections.Generic.List<string>();
            var words = text.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (text.Length <= maxCharactersPerLine || words.Length == 1)
            {
                result.Add(text);
            }
            else
            {
                foreach (var word in words)
                {
                    var addition = $" {word.Trim()}";
                    var lineIndex = result.Count - 1;
                    var lineCharacters = lineIndex > -1 ? result[lineIndex].Length + addition.Length : 0;

                    if (result.Count < 1 || lineCharacters > maxCharactersPerLine)
                    {
                        // Start new line
                        addition = addition.Trim();
                        result.Add(addition);
                    }
                    else
                    {
                        // Append existing line
                        result[lineIndex] += addition;
                    }
                }
            }
            return result;
        }

        public static System.Collections.Generic.List<string> WordWrap2(this string text, int maxCharactersPerLine, int numberOfItems)
        {
            text = text.Trim();
            var result = new System.Collections.Generic.List<string>();
            var words = text.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var i = 0;

            if (text.Length <= maxCharactersPerLine || words.Length == 1)
            {
                result.Add(text);
            }
            else
            {
                foreach (var word in words)
                {
                    var addition = $" {word.Trim()}";
                    var lineIndex = result.Count - 1;
                    var lineCharacters = lineIndex > -1 ? result[lineIndex].Length + addition.Length : 0;

                    if (result.Count < 1 || lineCharacters > maxCharactersPerLine )
                    {
                        // Start new line
                        i++;
                        addition = addition.Trim();
                        if (i <= numberOfItems)
                        {
                            result.Add(addition);
                        }
                        else
                        {
                            // Append existing line
                            result[lineIndex] += " " + addition;
                        }
                    }
                    else
                    {
                        // Append existing line
                        result[lineIndex] += addition;
                    }
                }
            }
            return result;
        }
    }
}
