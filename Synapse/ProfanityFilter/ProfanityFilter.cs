/*
MIT License
Copyright (c) 2019
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Synapse.ProfanityFilter;

/// <summary>
///     This class will detect profanity and racial slurs contained within some text and return an indication flag.
///     All words are treated as case insensitive.
/// </summary>
public class ProfanityFilter : ProfanityBase
{
    /// <summary>
    ///     For a given sentence, look for the specified profanity. If it is found, look to see
    ///     if it is part of a containing word. If it is, then return the containing work and the start
    ///     and end positions of that word in the string.
    ///     For example, if the string contains "scunthorpe" and the passed in profanity is "cunt",
    ///     then this method will find "cunt" and work out that it is part of an enclosed word.
    /// </summary>
    /// <param name="toCheck">Sentence to check.</param>
    /// <param name="profanity">Profanity to look for.</param>
    /// <returns>
    ///     Tuple of the following format (start character, end character, found enclosed word).
    ///     If no enclosed word is found then return null.
    /// </returns>
    public static (int, int, string)? GetCompleteWord(string toCheck, string profanity)
    {
        if (string.IsNullOrEmpty(toCheck))
        {
            return null;
        }

        string profanityLowerCase = profanity.ToLower(CultureInfo.InvariantCulture);
        string toCheckLowerCase = toCheck.ToLower(CultureInfo.InvariantCulture);

        if (!toCheckLowerCase.Contains(profanityLowerCase))
        {
            return null;
        }

        int startIndex = toCheckLowerCase.IndexOf(profanityLowerCase, StringComparison.Ordinal);
        int endIndex = startIndex;

        // Work backwards in string to get to the start of the word.
        while (startIndex > 0)
        {
            if (toCheck[startIndex - 1] == ' ' || char.IsPunctuation(toCheck[startIndex - 1]))
            {
                break;
            }

            startIndex -= 1;
        }

        // Work forwards to get to the end of the word.
        while (endIndex < toCheck.Length)
        {
            if (toCheck[endIndex] == ' ' || char.IsPunctuation(toCheck[endIndex]))
            {
                break;
            }

            endIndex += 1;
        }

        return (startIndex, endIndex,
            toCheckLowerCase.Substring(startIndex, endIndex - startIndex).ToLower(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     For any given string, censor any profanities from the list using the specified
    ///     censoring character.
    /// </summary>
    /// <param name="sentence">The string to censor.</param>
    /// <param name="censorCharacter">The character to use for censoring.</param>
    /// <returns>A censored string</returns>
    public string CensorString(string sentence, char censorCharacter = '*')
    {
        return CensorString(sentence, censorCharacter, false);
    }

    /// <summary>
    ///     For any given string, censor any profanities from the list using the specified
    ///     censoring character.
    /// </summary>
    /// <param name="sentence">The string to censor.</param>
    /// <param name="censorCharacter">The character to use for censoring.</param>
    /// <param name="ignoreNumbers">Ignore any numbers that appear in a word.</param>
    /// <returns>A censored string</returns>
    public string CensorString(string sentence, char censorCharacter, bool ignoreNumbers)
    {
        if (string.IsNullOrEmpty(sentence))
        {
            return string.Empty;
        }

        string noPunctuation = sentence.Trim();
        noPunctuation = noPunctuation.ToLower();

        noPunctuation = Regex.Replace(noPunctuation, @"[^\w\s]", string.Empty);

        string[] words = noPunctuation.Split(' ');

        List<string> postAllowList = FilterWordListByAllowList(words);
        List<string> swearList = [];

        // Catch whether multi-word profanities are in the allow list filtered sentence.
        AddMultiWordProfanities(swearList, ConvertWordListToSentence(postAllowList));

        StringBuilder censored = new(sentence);
        StringBuilder tracker = new(sentence);

        return CensorStringByProfanityList(censorCharacter, swearList, censored, tracker, ignoreNumbers).ToString();
    }

    private static StringBuilder CensorStringByProfanityList(
        char censorCharacter,
        IEnumerable<string> swearList,
        StringBuilder censored,
        StringBuilder tracker,
        bool ignoreNumeric)
    {
        foreach (string word in swearList.OrderByDescending(x => x.Length))
        {
            (int, int, string)? result = (0, 0, string.Empty);
            string[] multiWord = word.Split(' ');

            if (multiWord.Length == 1)
            {
                do
                {
                    result = GetCompleteWord(tracker.ToString(), word);

                    if (result == null)
                    {
                        continue;
                    }

                    string filtered = result.Value.Item3;

                    if (ignoreNumeric)
                    {
                        filtered = Regex.Replace(result.Value.Item3, @"[\d-]", string.Empty);
                    }

                    if (filtered == word)
                    {
                        for (int i = result.Value.Item1; i < result.Value.Item2; i++)
                        {
                            censored[i] = censorCharacter;
                            tracker[i] = censorCharacter;
                        }
                    }
                    else
                    {
                        for (int i = result.Value.Item1; i < result.Value.Item2; i++)
                        {
                            tracker[i] = censorCharacter;
                        }
                    }
                }
                while (result != null);
            }
            else
            {
                censored = censored.Replace(word, CreateCensoredString(word, censorCharacter));
            }
        }

        return censored;
    }

    private static string ConvertWordListToSentence(IEnumerable<string> postAllowList)
    {
        // Reconstruct sentence excluding allow listed words.
        return postAllowList.Aggregate(string.Empty, (current, w) => current + w + " ");
    }

    private static string CreateCensoredString(string word, char censorCharacter)
    {
        string censoredWord = string.Empty;

        foreach (char t in word)
        {
            if (t != ' ')
            {
                censoredWord += censorCharacter;
            }
            else
            {
                censoredWord += ' ';
            }
        }

        return censoredWord;
    }

    private static List<string> FilterWordListByAllowList(IEnumerable<string> words)
    {
        return words
            .Where(word => !string.IsNullOrEmpty(word))
            .ToList();
    }

    private void AddMultiWordProfanities(List<string> swearList, string postAllowListSentence)
    {
        swearList.AddRange(
            from string profanity in Profanities
#pragma warning disable CA1862
            where postAllowListSentence.ToLower(CultureInfo.InvariantCulture).Contains(profanity)
#pragma warning restore CA1862
            select profanity);
    }
}
