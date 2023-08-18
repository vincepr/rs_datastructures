﻿using Microsoft.VisualBasic;
using src.algorithms.HuffmanCoding;
using src.datastructures.BinarySearchTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using System.Xml.Linq;
using File = System.IO.File;
using System.Buffers.Binary;

namespace src.algorithms.Deflate;

internal class GzipDecompress
{
    public static string GzipRun(string[] args)
    {
        if (args.Length != 2) return "Usage: GzipDecompress Inputfile.gz Outputfile";
        string inPath = args[0];
        string outPath = args[1];

        if (!File.Exists(inPath) || Directory.Exists(inPath)) return $"Input-File {inPath} not found!";
        
        using (var stream = File.OpenRead(inPath))
        {
            // StreamReader vs BinaryReader here?
            using (var reader = new BinaryReader(stream))
            {
                // 2 bytes initialisation - gzip magic number: 
                var magicNr = reader.ReadUInt16();
                if (!(magicNr == (UInt16)0x1F8B || magicNr == (UInt16)35615)) return "Invalid gzip magic number.";
                // 1 byte compression-method -  Deflate = 8 for deflate
                byte compressionMethod = reader.ReadByte();
                if (compressionMethod != (Byte)8) return $"Unsupported compression method. Expected 8 got: [{compressionMethod}].";
                // 1 byte special-info - reserved-bits must be 0
                BitVector32 fileFlags = new BitVector32(reader.ReadByte());    //reader.ReadInt32() any difference?;
                if (fileFlags[5] || fileFlags[6] || fileFlags[7]) return "Reserved flags are set. Must be 0";
                // 4 byte unixtimestamp - last modified - time is in endian byte array -> we reverse it before casting it
                int unixTime = readLittleEndianInt32(reader); //BitConverter.ToInt32(reader.ReadBytes(4).ToArray());
                var dateTimeLastModification = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                if (unixTime != 0) Console.WriteLine($"Last modified - {dateTimeLastModification}");
                else Console.WriteLine("last modified - N/A");
                // 1 byte additional-info - info about kompression
                BitVector32 extraFlags = new BitVector32(reader.ReadByte());
                if (extraFlags[2]) Console.WriteLine("Compression - maximal Compression and slowest algorithm.");
                else if (extraFlags[4]) Console.WriteLine("Compression - fastest Compression algorithm.");
                else Console.WriteLine($"Compression unknown. Extra-flags: {extraFlags}");
                // 1 byte os-file-system - info about what OS this file was compressed on
                byte operatingSystem = reader.ReadByte();
                string os = operatingSystem switch
                {
                    0 => "FAT filesystem (MS-DOS, OS/2, NT/Win32)",
                    1 => "Amiga",
                    2 => "VMS (or OpenVMS)",
                    3 => "Unix",
                    4 => "VM/CMS",
                    5 => "Atari TOS",
                    6 => "HPFS filesystem (OS/2, NT)",
                    7 => "Macintosh",
                    8 => "Z-System",
                    9 => "CP/M",
                    10 => "TOPS-20",
                    11 => "NTFS filesystem (NT)",
                    12 => "QDOS",
                    13 => "Acorn RISCOS",
                    255 => "Unknown",
                    _ => "Could not match OperatingSystem Identifier",
                };
                Console.WriteLine($"File-System-Info - value: {operatingSystem} => {os}");
                // next come optinal flags, denoted in fileFlags.
                //  0x01    FTEXT       file is probably ASCII text.
                //  0x04    FEXTRA      The file contains extra fields
                //  0x08    FNAME       The file contains an original file name string
                //  0x10    FCOMMENT    The file contains comment
                //  0x20                Reserved
                //  0x40                Reserved
                //  0x80                Reserved
                if (fileFlags[1]) Console.WriteLine("Flag0 FTEXT - Indicating this is Text is set.");
                if (fileFlags[4]) 
                {
                    byte[] u16endian = reader.ReadBytes(2);
                    var bytesToSkipp = BinaryPrimitives.ReadUInt16LittleEndian(u16endian);
                    Console.WriteLine($"Flag2 FEXTRA - Indicating Extra");
                    reader.ReadBytes(bytesToSkipp);
                }
                if (fileFlags[8]) Console.WriteLine($"Flag3 FNAME- Indicating File name: {readNullTerminatedString(reader)}");
                // TODO: check if fileFlags[16] is set properly
                if (fileFlags[16]) Console.WriteLine($"Flag4 FCOMMENT - Indicating Comment: {readNullTerminatedString(reader)}");
                if (fileFlags[2]) 
                {
                    reader.ReadBytes(2); // 2 byte checksum (that we just disregard)
                    Console.WriteLine("Flag1 FHCRC - Indicating this has a header-checksum is set.");
                }
                Stream underlyingStream = reader.BaseStream;
                Stream output = new MemoryStream();
                BitStream bitwiseInStream = new BitStream(underlyingStream);

                // try to decompress
                try
                {
                    Decompressor.Decompress(bitwiseInStream, output);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Error: {e.Message}");
                }
                // read footer and check checksumm
                int crc = readLittleEndianInt32(reader);
                int size = readLittleEndianInt32(reader);

                //if (size != output.Length)
                //    return $"Size mismatched. expected: {size} got: {output.Length}";
                // TODO: check if calculated-crc == read crc-checksum matches up
                
                //dbgPrintOutStream(output);
                dbgWriteStreamToFile(output, outPath);
            };
        }
        return "finished successfully";
    }

    private static void dbgWriteStreamToFile(Stream output, string newFilePath)
    {
        using (var fileStream = File.Create(newFilePath))
        {
            output.Seek(0, SeekOrigin.Begin);
            output.CopyTo(fileStream);
        }

    }

    private static int readLittleEndianInt32(BinaryReader reader)
    {
        return BitConverter.ToInt32(reader.ReadBytes(4).ToArray());
    }
    
    private static void dbgPrintOutStream(Stream output)
    {
        // Debuging only till we get anything working
        // print out contents we decoded (works for text only)
        output.Position = 0;
        StreamReader toStrReader = new StreamReader(output);
        string txt = toStrReader.ReadToEnd();
        Console.WriteLine(txt);
    }
    
    private static string readNullTerminatedString(in BinaryReader reader)
    {
        char ch = reader.ReadChar();
        string result = "";
        while (ch != '\0')
        {
            result += ch;
            ch = reader.ReadChar();
        }
        return result;
    }
}