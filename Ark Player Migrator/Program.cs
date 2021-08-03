using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ArkSavegameToolkitNet;
using ArkSavegameToolkitNet.DataTypes;
using ArkSavegameToolkitNet.DataTypes.Properties;
using ArkSavegameToolkitNet.Domain;
using ArkSavegameToolkitNet.Utils.Extensions;

namespace Ark_Player_Migrator
{
    class Program
    {
        public class ProfileData
        {
            public int PlayerDataIdStart, PlayerDataIdEnd, UniqueIdStart, UniqueIdEnd;
            public byte[] Data, PlayerDataBytes, UniqueIdBytes;
            public int FileLength, DisplayId;
            public string DisplayNetId;

            /// <summary>
            /// Reads arkprofile file, and collects player data
            /// </summary>
            public ProfileData(string path)
            {
                // Read input file
                Data = File.ReadAllBytes(path);
                FileLength = Data.Length;

                // Get UInt64Property
                PlayerDataIdStart = FindByteArrEnd(Data, Encoding.ASCII.GetBytes("UInt64Property"));
                PlayerDataIdEnd = FindByteArr(Data, Encoding.ASCII.GetBytes("UniqueID"), PlayerDataIdStart);
                PlayerDataBytes = new byte[PlayerDataIdEnd - PlayerDataIdStart];
                Buffer.BlockCopy(Data, PlayerDataIdStart, PlayerDataBytes, 0, PlayerDataBytes.Length);
                TrimPlayerData();

                // Get UniqueID
                UniqueIdStart = FindByteArrEnd(Data, Encoding.ASCII.GetBytes("UniqueNetIdRepl"), PlayerDataIdEnd);
                UniqueIdEnd = FindByteArr(Data, Encoding.ASCII.GetBytes("SavedNetworkAddress"), UniqueIdStart);
                UniqueIdBytes = new byte[UniqueIdEnd - UniqueIdStart];
                Buffer.BlockCopy(Data, UniqueIdStart, UniqueIdBytes, 0, UniqueIdBytes.Length);
                DisplayNetId = Regex.Replace(Encoding.ASCII.GetString(UniqueIdBytes, 0, UniqueIdBytes.Length), "[^a-zA-Z0-9]", string.Empty);
            }

            /// <summary>
            /// Creates easy-to-read integer representation of PlayerData bytes
            /// </summary>
            private void TrimPlayerData()
            {
                int start = 0, end = 0;
                for (var i = 5; i < PlayerDataBytes.Length - 5; i++)
                {
                    if (PlayerDataBytes[i] == 0x00) continue;
                    if (start == 0)
                    {
                        start = i;
                        continue;
                    }

                    end = i + 1;
                }

                var playerDataIntBytes = new byte[end - start];
                Buffer.BlockCopy(PlayerDataBytes, start, playerDataIntBytes, 0, playerDataIntBytes.Length);
                DisplayId = BitConverter.ToInt32(playerDataIntBytes);
            }

            /// <summary>
            /// Returns the index for start of a byte array in a byte array
            /// </summary>
            private static int FindByteArr(byte[] src, byte[] find, int offset = 0)
            {
                // Find needle in haystack
                for (var i = offset; i < src.Length; i++)
                {
                    // Check for first byte match
                    if (src[i] != find[0]) continue;

                    // Check rest of byte array for match
                    var found = false;
                    for (var j = 0; j < find.Length; j++)
                    {
                        if (src[i + j] != find[j])
                        {
                            // Not a match
                            found = false;
                            break;
                        }

                        found = true;
                    }

                    if (found) // Found what we're looking for!
                        return i;
                }

                return -1;
            }

            /// <summary>
            /// Returns the index for end of a byte array in a byte array
            /// </summary>
            private static int FindByteArrEnd(byte[] src, byte[] find, int offset = 0)
            {
                return FindByteArr(src, find, offset) + find.Length;
            }
        }

        static void Main(string[] args)
        {
            var profileFrom = new ProfileData("C:\\temp\\76561198135744397.arkprofile");
            var profileTo = new ProfileData("C:\\temp\\76561198064588130.arkprofile");
            Console.WriteLine("Copying data:");
            Console.WriteLine("- From: " + profileFrom.DisplayId + " -- " + profileFrom.DisplayNetId);
            Console.WriteLine("- Into: " + profileTo.DisplayId + " -- " + profileTo.DisplayNetId);

            List<byte> outBytes = new();
            // Copy first block, from start to UInt64Property
            outBytes.AddRange(profileFrom.Data.Take(profileFrom.PlayerDataIdStart));
            // Copy new UInt64Property
            outBytes.AddRange(profileTo.PlayerDataBytes);
            // Copy from end of UInt64Property to start of UniqueNetIdRepl
            outBytes.AddRange(profileFrom.Data.Skip(profileFrom.PlayerDataIdEnd).Take(profileFrom.UniqueIdStart - profileFrom.PlayerDataIdEnd));
            // Copy PlayerDataBytes
            outBytes.AddRange(profileTo.UniqueIdBytes);
            // Copy from end of UnqiueNewIdRepl to end of file
            outBytes.AddRange(profileFrom.Data.Skip(profileFrom.UniqueIdEnd).Take(profileFrom.Data.Length - profileFrom.UniqueIdEnd));

            File.WriteAllBytes("C:\\temp\\76561198064588130.arkprofile", outBytes.ToArray());
        }
    }
}
