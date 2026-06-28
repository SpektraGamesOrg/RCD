using System;
using TMPro;
using UnityEngine;

namespace UISystem.Runtime.Scripts
{
    public static class TextWidgetExtensions
    {
        /// <summary>
        /// Converts an integer value to a character array and sets it as the text of a TMP_Text component.
        /// </summary>
        /// <param name="textComponent">The TMP_Text component to set the text on.</param>
        /// <param name="value">The integer value to convert.</param>
        /// <param name="digitCount">The number of digits in the integer value.</param>
        /// <param name="charArray">The character array to store the converted value.</param>
        /// <param name="withNew">Whether to create a new character array.</param>
        public static void TMP_Text_IntConverter(this TMP_Text textComponent, int value, int digitCount,
            ref char[] charArray, bool withNew = false)
        {
            if (textComponent == null)
                return;

            value.ConvertToCharArray(ref digitCount, ref charArray, withNew);
            textComponent.SetText(charArray, 0, digitCount);
        }

        /// <summary>
        /// Gets the number of digits in an integer.
        /// </summary>
        /// <param name="number">The integer value.</param>
        /// <returns>The number of digits in the integer.</returns>
        public static int GetDigitCount(this int number)
        {
            // Handle negative numbers
            number = Math.Abs(number);

            // Special case for 0
            if (number == 0)
                return 1;

            // Count digits dynamically
            var digitCount = 0;
            while (number > 0)
            {
                number /= 10;
                digitCount++;
            }

            return digitCount;
        }

        /// <summary>
        /// Converts an integer value to a character array.
        /// </summary>
        /// <param name="number">The integer value to convert.</param>
        /// <param name="digitCount">The number of digits in the integer value.</param>
        /// <param name="digitsArray">The character array to store the converted value.</param>
        /// <param name="withNew">Whether to create a new character array.</param>
        private static void ConvertToCharArray(this int number, ref int digitCount, ref char[] digitsArray, bool withNew)
        {
            // Handle negative numbers
            number = Mathf.Abs(number);

            // Count digits dynamically
            // Populate the char array
            if (withNew)
            {
                digitsArray = new char[digitCount];
            }

            var temp = number;
            for (var i = digitCount - 1; i >= 0; i--)
            {
                digitsArray[i] = (char)('0' + (temp % 10));
                temp /= 10;
            }

            // Special case for 0
            if (digitCount != 0) 
                return;
            
            digitCount = 1;
            digitsArray = new char[] { '0' };
        }

        /// <summary>
        /// Converts a total number of seconds to a time format and sets it as the text of a TMP_Text component.
        /// </summary>
        /// <param name="textComponent">The TMP_Text component to set the text on.</param>
        /// <param name="inputTotalSeconds">The total number of seconds to convert.</param>
        /// <param name="charArray">The character array to store the converted value.</param>
        public static void TMP_Text_ConvertSecondsToTimeFormat(this TMP_Text textComponent, double inputTotalSeconds,
            ref char[] charArray)
        {
            if (textComponent == null)
                return;

            ConvertSecondsToTimeFormat(inputTotalSeconds, ref charArray, out int length);

            textComponent.SetText(charArray, 0, length);
        }

        /// <summary>
        /// Gets the number of days from a total number of seconds.
        /// </summary>
        /// <param name="totalSeconds">The total number of seconds.</param>
        /// <returns>The number of days.</returns>
        private static int GetDays(this int totalSeconds)
        {
            return totalSeconds / 86400;
        }
        
        /// <summary>
        /// Gets the number of hours from a total number of seconds.
        /// </summary>
        /// <param name="totalSeconds">The total number of seconds.</param>
        /// <returns>The number of hours.</returns>
        private static int GetHours(this int totalSeconds)
        {
            return (totalSeconds % 86400) / 3600;
        }
        
        /// <summary>
        /// Gets the number of minutes from a total number of seconds.
        /// </summary>
        /// <param name="totalSeconds">The total number of seconds.</param>
        /// <returns>The number of minutes.</returns>
        private static int GetMinutes(this int totalSeconds)
        {
            return (totalSeconds % 3600) / 60;
        }
        
        /// <summary>
        /// Gets the number of seconds from a total number of seconds.
        /// </summary>
        /// <param name="totalSeconds">The total number of seconds.</param>
        /// <returns>The number of seconds.</returns>
        private static int GetSeconds(this int totalSeconds)
        {
            return totalSeconds % 60;
        }

        /// <summary>
        /// Converts a total number of seconds to a time format and stores it in a character array.
        /// </summary>
        /// <param name="inputTotalSeconds">The total number of seconds to convert.</param>
        /// <param name="charArray">The character array to store the converted value.</param>
        /// <param name="length">The length of the converted value.</param>
        private static void ConvertSecondsToTimeFormat(double inputTotalSeconds, ref char[] charArray, out int length)
        {
            if (charArray == null || charArray.Length < 15)
            {
                charArray = new char[15];
            }
        
            inputTotalSeconds = Math.Max(0, inputTotalSeconds);
            var totalSeconds = (int)inputTotalSeconds;
            var days = GetDays(totalSeconds);
            var hours = GetHours(totalSeconds);
            var minutes = GetMinutes(totalSeconds);
            var seconds = GetSeconds(totalSeconds);
        
            var charArrayIndex = 0;
            if (days > 0)
            {
                WriteNumberToCharArray(days, ref charArray, ref charArrayIndex);
                charArray[charArrayIndex++] = 'd';
                charArray[charArrayIndex++] = ' ';
                if (hours > 0) WriteTwoDigitNumberToCharArray(hours, ref charArray, ref charArrayIndex, 'h');
            }
            else if (hours > 0)
            {
                WriteTwoDigitNumberToCharArray(hours, ref charArray, ref charArrayIndex, 'h');
                if (minutes > 0) WriteTwoDigitNumberToCharArray(minutes, ref charArray, ref charArrayIndex, 'm');
            }
            else if (minutes > 0)
            {
                WriteTwoDigitNumberToCharArray(minutes, ref charArray, ref charArrayIndex, 'm');
                if (seconds > 0) WriteTwoDigitNumberToCharArray(seconds, ref charArray, ref charArrayIndex, 's');
            }
            else
            {
                WriteTwoDigitNumberToCharArray(seconds, ref charArray, ref charArrayIndex, 's');
            }
        
            length = charArrayIndex;
        }
        
        /// <summary>
        /// Writes a number to a character array.
        /// </summary>
        /// <param name="number">The number to write.</param>
        /// <param name="charArray">The character array to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        private static void WriteNumberToCharArray(int number, ref char[] charArray, ref int index)
        {
            if (number >= 100) charArray[index++] = (char)('0' + number / 100);
            if (number >= 10) charArray[index++] = (char)('0' + (number / 10) % 10);
            charArray[index++] = (char)('0' + number % 10);
        }
        
        /// <summary>
        /// Writes a two-digit number to a character array with an optional suffix.
        /// </summary>
        /// <param name="number">The number to write.</param>
        /// <param name="charArray">The character array to write to.</param>
        /// <param name="index">The index to start writing at.</param>
        /// <param name="suffix">The optional suffix to add.</param>
        private static void WriteTwoDigitNumberToCharArray(int number, ref char[] charArray, ref int index, char? suffix = null)
        {
            if (number >= 10) charArray[index++] = (char)('0' + number / 10);
            charArray[index++] = (char)('0' + number % 10);
            if (suffix.HasValue) charArray[index++] = suffix.Value;
        }
    }
}