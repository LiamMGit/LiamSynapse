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

namespace Synapse.ProfanityFilter
{
    public partial class ProfanityBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfanityBase"/> class.
        /// Constructor that initializes the standard profanity list.
        /// </summary>
        protected ProfanityBase()
        {
            Profanities = new List<string>(_wordList);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfanityBase"/> class.
        /// Constructor that allows you to insert a custom array or profanities.
        /// This list will replace the default list.
        /// </summary>
        /// <param name="profanityList">Array of words considered profanities.</param>
        protected ProfanityBase(string[] profanityList)
        {
            if (profanityList == null)
            {
                throw new ArgumentNullException(nameof(profanityList));
            }

            Profanities = new List<string>(profanityList);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfanityBase"/> class.
        /// Constructor that allows you to insert a custom list or profanities.
        /// This list will replace the default list.
        /// </summary>
        /// <param name="profanityList">List of words considered profanities.</param>
        protected ProfanityBase(List<string> profanityList)
        {
            Profanities = profanityList ?? throw new ArgumentNullException(nameof(profanityList));
        }

        /// <summary>
        /// Return the number of profanities in the system.
        /// </summary>
        public int Count => Profanities.Count;

        protected List<string> Profanities { get; }

        /// <summary>
        /// Add a custom profanity to the list.
        /// </summary>
        /// <param name="profanity">The profanity to add.</param>
        public void AddProfanity(string profanity)
        {
            if (string.IsNullOrEmpty(profanity))
            {
                throw new ArgumentNullException(nameof(profanity));
            }

            Profanities.Add(profanity);
        }

        /// <summary>
        /// Add a custom array profanities to the defaultl list. This adds to the
        /// default list, and does not replace it.
        /// </summary>
        /// <param name="profanityList">The array of profanities to add.</param>
        public void AddProfanity(string[] profanityList)
        {
            if (profanityList == null)
            {
                throw new ArgumentNullException(nameof(profanityList));
            }

            Profanities.AddRange(profanityList);
        }

        /// <summary>
        /// Add a custom list profanities to the defaultl list. This adds to the
        /// default list, and does not replace it.
        /// </summary>
        /// <param name="profanityList">The list of profanities to add.</param>
        public void AddProfanity(List<string> profanityList)
        {
            if (profanityList == null)
            {
                throw new ArgumentNullException(nameof(profanityList));
            }

            Profanities.AddRange(profanityList);
        }

        /// <summary>
        /// Remove a profanity from the current loaded list of profanities.
        /// </summary>
        /// <param name="profanity">The profanity to remove from the list.</param>
        /// <returns>True of the profanity was removed. False otherwise.</returns>
        public bool RemoveProfanity(string profanity)
        {
            if (string.IsNullOrEmpty(profanity))
            {
                throw new ArgumentNullException(nameof(profanity));
            }

            return Profanities.Remove(profanity.ToLower(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Remove a list of profanities from the current loaded list of profanities.
        /// </summary>
        /// <param name="profanities">The list of profanities to remove from the list.</param>
        /// <returns>True if the profanities were removed. False otherwise.</returns>
        public bool RemoveProfanity(List<string> profanities)
        {
            if (profanities == null)
            {
                throw new ArgumentNullException(nameof(profanities));
            }

            return profanities.All(RemoveProfanity);
        }

        /// <summary>
        /// Remove an array of profanities from the current loaded list of profanities.
        /// </summary>
        /// <param name="profanities">The array of profanities to remove from the list.</param>
        /// <returns>True if the profanities were removed. False otherwise.</returns>
        public bool RemoveProfanity(string[] profanities)
        {
            if (profanities == null)
            {
                throw new ArgumentNullException(nameof(profanities));
            }

            return profanities.All(RemoveProfanity);
        }

        /// <summary>
        /// Remove all profanities from the current loaded list.
        /// </summary>
        public void Clear()
        {
            Profanities.Clear();
        }
    }
}
