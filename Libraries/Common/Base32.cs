using System;
using System.Text;

namespace Richter.Utilities {
   public static class Base32 {
      private const Int32 c_InByteSize = 8;
      private const Int32 c_OutByteSize = 5;
      private const String Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

      /// <summary>Convert byte array to Base32 format.</summary>
      /// <param name="bytes">An array of bytes to convert to Base32 format</param>
      /// <returns>Returns a string representing byte array</returns>
      public static String ToBase32String(this Byte[] bytes) {
         if (bytes == null || bytes.Length == 0) throw new ArgumentException("bytes");

         StringBuilder builder = new StringBuilder(bytes.Length * c_InByteSize / c_OutByteSize);

         // Position in the input buffer
         Int32 bytesPosition = 0;

         // Offset inside a single byte that <bytesPosition> points to (from left to right)
         // 0 - highest bit, 7 - lowest bit
         Int32 bytesSubPosition = 0;

         // Byte to look up in the dictionary
         Byte outputBase32Byte = 0;

         // The number of bits filled in the current output byte
         Int32 outputBase32BytePosition = 0;

         // Iterate through input buffer until we reach past the end of it
         while (bytesPosition < bytes.Length) {
            // Calculate the number of bits we can extract out of current input byte to fill missing bits in the output byte
            Int32 bitsAvailableInByte = Math.Min(c_InByteSize - bytesSubPosition, c_OutByteSize - outputBase32BytePosition);

            // Make space in the output byte
            outputBase32Byte <<= bitsAvailableInByte;

            // Extract the part of the input byte and move it to the output byte
            outputBase32Byte |= (Byte)(bytes[bytesPosition] >> (c_InByteSize - (bytesSubPosition + bitsAvailableInByte)));

            // Update current sub-byte position
            bytesSubPosition += bitsAvailableInByte;

            // Check overflow
            if (bytesSubPosition >= c_InByteSize) { // Move to the next byte
               bytesPosition++; bytesSubPosition = 0;
            }

            // Update current base32 byte completion
            outputBase32BytePosition += bitsAvailableInByte;

            // Check overflow or end of input array
            if (outputBase32BytePosition >= c_OutByteSize) {
               // Drop the overflow bits
               outputBase32Byte &= 0x1F;  // 0x1F = 00011111 in binary

               // Add current Base32 byte and convert it to character
               builder.Append(Base32Alphabet[outputBase32Byte]);

               // Move to the next byte
               outputBase32BytePosition = 0;
            }
         }

         // Check if we have a remainder
         if (outputBase32BytePosition > 0) {
            // Move to the right bits
            outputBase32Byte <<= (c_OutByteSize - outputBase32BytePosition);

            // Drop the overflow bits
            outputBase32Byte &= 0x1F;  // 0x1F = 00011111 in binary

            // Add current Base32 byte and convert it to character
            builder.Append(Base32Alphabet[outputBase32Byte]);
         }
         return builder.ToString();
      }

      /// <summary>Convert base32 string to array of bytes</summary>
      /// <param name="base32String">Base32 string to convert</param>
      /// <returns>Returns a byte array converted from the string</returns>
      public static Byte[] FromBase32String(this String base32String) {
         if (base32String == null) throw new ArgumentNullException("base32String");
         {
            if (base32String == String.Empty) return new byte[0];

            // Convert to upper-case
            String base32StringUpperCase = base32String.ToUpperInvariant();

            // Prepare output byte array
            Byte[] outputBytes = new Byte[base32StringUpperCase.Length * c_OutByteSize / c_InByteSize];

            // Check the size
            if (outputBytes.Length == 0) {
               throw new ArgumentException("Specified string is not valid Base32 format because it doesn't have enough data to construct a complete byte array");
            }

            // Position in the string
            Int32 base32Position = 0;

            // Offset inside the character in the string
            Int32 base32SubPosition = 0;

            // Position within outputBytes array
            Int32 outputBytePosition = 0;

            // The number of bits filled in the current output byte
            Int32 outputByteSubPosition = 0;

            // Normally we would iterate on the input array but in this case we actually iterate on the output array
            // We do it because output array doesn't have overflow bits, while input does and it will cause output array overflow if we don't stop in time
            while (outputBytePosition < outputBytes.Length) {
               // Look up current character in the dictionary to convert it to byte
               Int32 currentBase32Byte = Base32Alphabet.IndexOf(base32StringUpperCase[base32Position]);

               // Check if found
               if (currentBase32Byte < 0) {
                  throw new ArgumentException(String.Format("Specified string is not valid Base32 format because character \"{0}\" does not exist in Base32 alphabet", base32String[base32Position]));
               }

               // Calculate the number of bits we can extract out of current input character to fill missing bits in the output byte
               Int32 bitsAvailableInByte = Math.Min(c_OutByteSize - base32SubPosition, c_InByteSize - outputByteSubPosition);

               // Make space in the output byte
               outputBytes[outputBytePosition] <<= bitsAvailableInByte;

               // Extract the part of the input character and move it to the output byte
               outputBytes[outputBytePosition] |= (Byte)(currentBase32Byte >> (c_OutByteSize - (base32SubPosition + bitsAvailableInByte)));

               // Update current sub-byte position
               outputByteSubPosition += bitsAvailableInByte;

               // Check overflow
               if (outputByteSubPosition >= c_InByteSize) { // Move to the next byte
                  outputBytePosition++; outputByteSubPosition = 0;
               }

               // Update current base32 byte completion
               base32SubPosition += bitsAvailableInByte;

               // Check overflow or end of input array
               if (base32SubPosition >= c_OutByteSize) { // Move to the next character
                  base32Position++;
                  base32SubPosition = 0;
               }
            }
            return outputBytes;
         }
      }
   }
}