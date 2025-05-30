﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Pulsar_Pack_Creator.MainWindow;


namespace Pulsar_Pack_Creator.IO
{
    class Builder : IOBase
    {
        public enum BuildParams
        {
            ConfigOnly,
            ConfigAndTracks,
            Full
        }
        public Builder(MainWindow window, BuildParams buildParams, bool createXML) : base(window)
        {
            modFolder = $"output/{parameters.modFolderName}temp";
            cups = window.cups.AsReadOnly();

            date = window.date;
            regsExperts = window.regsExperts;

            trackNamesTuple = window.SortTracks();

            this.buildParams = buildParams;
            this.createXML = createXML;

            string[] info = Directory.GetFiles("input/", "*", SearchOption.AllDirectories);
            inputFiles = info.Select(s => s.Replace('\\', '/').ToLowerInvariant()).ToArray();
        }


        BigEndianWriter bin = null;
        StreamWriter bmgSW = null;
        StreamWriter fileSW = null;
        StreamWriter crcToFile = null;
        //BigEndianWriter textSW = null;
        ushort[] trophyCount;


        ReadOnlyCollection<MainWindow.Cup> cups;
        readonly string[,,] regsExperts;
        readonly (string[], string[]) trackNamesTuple;
        readonly BuildParams buildParams;
        readonly bool createXML;
        readonly string[] inputFiles;
        readonly string modFolder;
        readonly string date;

        PulsarGame.BinaryHeader configHeader = new PulsarGame.BinaryHeader(configMagic, (int)CONFIGVERSION);
        PulsarGame.InfoHolder infoSection = new PulsarGame.InfoHolder(infoMagic, INFOVERSION);
        PulsarGame.CupsHolderV3 cupsSection = new PulsarGame.CupsHolderV3(cupsMagic, CUPSVERSION);
        List<PulsarGame.TrackV3> mainTracksList = new List<PulsarGame.TrackV3>();
        List<PulsarGame.Variant> variantTracksList = new List<PulsarGame.Variant>();
        //List<(string, int)> stringPool = new List<(string, int)>();

        public Result Build()
        {

            if (date == null) return Result.NoDate;
            if (parameters.modFolderName == null || parameters.modFolderName == "") return Result.NoModName;
            if (parameters.wiimmfiRegion == -1) return Result.NoWiimmfi;

            if (Directory.Exists(modFolder))
            {
                try { Directory.Delete(modFolder, true); }
                catch { return Result.AlreadyInUse; }
            }
            Directory.CreateDirectory("output");
            Directory.CreateDirectory(modFolder);
            Directory.CreateDirectory($"{modFolder}/Binaries");
            Directory.CreateDirectory($"{modFolder}/Tracks");
            Directory.CreateDirectory($"{modFolder}/Ghosts");
            Directory.CreateDirectory($"{modFolder}/Ghosts/Experts");
            //Scene用のフォルダを作成.
            Directory.CreateDirectory($"{modFolder}/Scene");

            Directory.CreateDirectory("output/Riivolution");


            try
            {
                string[] bmgLines = File.ReadAllLines("temp/PulsarBMG.txt");
                string cc = parameters.has200cc ? "200" : "100";
                for (int i = 0; i < bmgLines.Length; i++)
                {
                    bmgLines[i] = bmgLines[i].Replace("{CC}", cc);
                    bmgLines[i] = bmgLines[i].Replace("{date}", $"{date}");
                }
                File.WriteAllLines("temp/BMG.txt", bmgLines);
                using (bin = new BigEndianWriter(File.Create("temp/Config.pul")))
                using (MemoryStream fileSectStream = new MemoryStream())
                {
                    using (bmgSW = new StreamWriter("temp/BMG.txt", true))
                    using (fileSW = new StreamWriter(fileSectStream))
                    using (crcToFile = new StreamWriter($"{modFolder}/Ghosts/FolderToTrackName.txt"))
                    //using (textSW = new BigEndianWriter(File.Create("temp/cupText.txt"), Encoding.Unicode))
                    {
                        bmgSW.WriteLine(bmgSW.NewLine);
                        fileSW.WriteLine("FILE");

                        Result infoRet = WriteInfo();
                        if (infoRet != Result.Success) return infoRet;

                        Result cupRet = WriteCups();
                        if (cupRet != Result.Success) return cupRet;

                    }
                    Result bmgRet = RequestBMGAction(true);
                    if (bmgRet != Result.Success) return bmgRet;




                    if (createXML) CreateXML();
                    if (buildParams != BuildParams.ConfigOnly)
                    {
                        File.WriteAllBytes($"{modFolder}/Binaries/Loader.pul", PulsarRes.Loader);
                        Directory.CreateDirectory($"{modFolder}/Assets");
                        Directory.CreateDirectory($"{modFolder}/CTBRSTM");
                        Directory.CreateDirectory($"{modFolder}/My Stuff");
                    }
                    if (buildParams == BuildParams.Full)
                    {
                        File.WriteAllBytes($"{modFolder}/Binaries/Code.pul", PulsarRes.Code);
                        File.WriteAllBytes($"{modFolder}/Assets/RaceAssets.szs", PulsarRes.RaceAssets);
                        File.WriteAllBytes($"{modFolder}/Assets/CommonAssets.szs", PulsarRes.CommonAssets);

                        //Scene用のファイルをコピー.
                        File.Copy("temp/Channel.szs", $"{modFolder}/Scene/Channel.szs", true);
                        File.Copy("temp/Earth.szs", $"{modFolder}/Scene/Earth.szs", true);
                        File.Copy("temp/Globe.szs", $"{modFolder}/Scene/Globe.szs", true);
                        File.Copy("temp/MenuMulti.szs", $"{modFolder}/Scene/MenuMulti.szs", true);
                        File.Copy("temp/MenuOther.szs", $"{modFolder}/Scene/MenuOther.szs", true);
                        File.Copy("temp/MenuSingle.szs", $"{modFolder}/Scene/MenuSingle.szs", true);
                        File.Copy("temp/Title.szs", $"{modFolder}/Scene/Title.szs", true);
                        File.Copy("temp/Title_J.szs", $"{modFolder}/Scene/Title_J.szs", true);

                        bool hasCustomIcons = false;
                        Process wimgtProcess = new Process();
                        ProcessStartInfo wimgtProcessInfo = new ProcessStartInfo();
                        wimgtProcessInfo.FileName = $"{wiimmFolderPath}wimgt.exe";
                        //wimgtProcessInfo.WorkingDirectory = @"temp/";
                        wimgtProcessInfo.CreateNoWindow = true;
                        wimgtProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        wimgtProcessInfo.UseShellExecute = false;
                        wimgtProcessInfo.RedirectStandardError = true;
                        wimgtProcess.StartInfo = wimgtProcessInfo;

                        List<(string, int)> customCupIcons = new List<(string, int)>(); //custom user icons + pulsar ones not in their correct spot
                        ushort extraIcons = 0; //custom user icons past cup 100
                        for (int i = 0; i < ctsCupCount; i++)
                        {
                            MainWindow.Cup cup = cups[i];
                            if (i >= 100 || cup.iconName != $"{MainWindow.Cup.defaultNames[i]}.png")
                            {
                                if (cup.iconName.Length <= 4)
                                {
                                    error = $"{cup.iconName} of cup {i + 1}";
                                    return Result.NoIcon;
                                }
                                hasCustomIcons = true;
                                bool isDefault = MainWindow.Cup.defaultNames.Contains(cup.iconName.Remove(cup.iconName.Length - 4));
                                string realIconName = isDefault ? $"temp/{cup.iconName}" : $"input/CupIcons/{cup.iconName}";
                                if (!File.Exists(realIconName))
                                {
                                    error = $"{realIconName} of cup {i + 1}";
                                    return Result.NoIcon;
                                }
                                customCupIcons.Add((realIconName, i));
                                if (i >= 100) ++extraIcons;
                            }
                            else if (cup.iconName == "") customCupIcons.Add((MainWindow.Cup.defaultNames[i], i));
                        }
                        if (hasCustomIcons)
                        {
                            int size = 128 / (1 + (customCupIcons.Count - 1) / 100);
                            foreach ((string, int) elem in customCupIcons)
                            {
                                using (Image image = Image.FromFile(elem.Item1))
                                {
                                    new Bitmap(image, size, size).Save($"temp/{elem.Item2}.png");
                                }
                                wimgtProcessInfo.Arguments = $"encode temp/{elem.Item2}.png --dest temp/UIAssets.d/button/timg/icon_{elem.Item2:D3}.tpl --transform CMPR -o";
                                wimgtProcess.Start();
                                error = wimgtProcess.StandardError.ReadToEnd();
                                if (error != "") return Result.WIMGT;
                                wimgtProcess.WaitForExit();
                            }
                            ProcessStartInfo wszstProcessInfo = new ProcessStartInfo();
                            wszstProcessInfo.FileName = @"wszst.exe";
                            wszstProcessInfo.Arguments = $"create temp/UIAssets.d --dest \"{modFolder}/Assets/UIAssets.szs\" -o";
                            wszstProcessInfo.CreateNoWindow = true;
                            wszstProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            wszstProcessInfo.UseShellExecute = false;
                            Process wszstProcess = new Process();
                            wszstProcess.StartInfo = wszstProcessInfo;
                            wszstProcess.Start();
                            wszstProcess.WaitForExit();
                            infoSection.info.cupIconCount = (ushort)(Math.Min((ushort)100, ctsCupCount) + extraIcons);
                        }
                        else File.Copy("temp/UIAssets.szs", $"{modFolder}/Assets/UIAssets.szs");
                    }

                    string gameFolderName = $"/{parameters.modFolderName}";
                    if (buildParams == BuildParams.ConfigOnly) configHeader.version *= -1;
                    configHeader.modFolderName = gameFolderName;
                    configHeader.offsetToInfo = Marshal.SizeOf(configHeader);
                    configHeader.offsetToCups = Marshal.SizeOf(infoSection) + configHeader.offsetToInfo;
                    configHeader.offsetToBMG = (int)(configHeader.offsetToCups + cupsSection.header.size);
                    byte[] rawHeader = PulsarGame.BytesFromStruct(configHeader);
                    byte[] rawInfo = PulsarGame.BytesFromStruct(infoSection);
                    byte[] rawCupsSection = PulsarGame.BytesFromStruct(cupsSection);

                    MemoryStream cupStream = new MemoryStream();
                    cupStream.Write(rawCupsSection);
                    for (int i = 0; i < mainTracksList.Count; i++)
                    {
                        cupStream.Write(PulsarGame.BytesFromStruct(mainTracksList[i]));
                    }
                    for (int i = 0; i < variantTracksList.Count; i++)
                    {
                        cupStream.Write(PulsarGame.BytesFromStruct(variantTracksList[i]));
                    }
                    using BigEndianReader bmgReader = new BigEndianReader(File.Open("temp/bmg.bmg", FileMode.Open));
                    bin.Write(rawHeader);
                    bin.Write(rawInfo);
                    bin.Write(cupStream.ToArray());
                    for (int i = 0; i < ctsCupCount * 4; i++)
                    {
                        bin.Write((ushort)Array.IndexOf(trackNamesTuple.Item2, trackNamesTuple.Item1[i]));
                    }
                    if (ctsCupCount % 2 == 1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            bin.Write((ushort)Array.IndexOf(trackNamesTuple.Item2, trackNamesTuple.Item1[i]));
                        }
                    }
                    bin.Write(bmgReader.ReadBytes((int)bmgReader.BaseStream.Length));
                    bin.Write(fileSectStream.ToArray());

                } //using memorystream
                File.Copy("temp/Config.pul", $"{modFolder}/Binaries/Config.pul", true);
                string finalDirName = $"output/{parameters.modFolderName}";
                if (Directory.Exists(finalDirName))
                {
                    try { Directory.Delete(finalDirName, true); }
                    catch { return Result.AlreadyInUse; }
                }
                Directory.Move(modFolder, finalDirName);
                return Result.Success;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return Result.UnknownError;
            }
            finally
            {
                File.Delete("temp/bmg.bmg");
                File.Delete("temp/bmg.txt");
                File.Delete("temp/Config.pul");
                File.Delete("temp/files.txt");
            }

            /*
            string test = "Test";
        byte[] a = System.Text.Encoding.Unicode.GetBytes(test);

        //reserved in case it is needed 
        //bin.BaseStream.Position = (bin.BaseStream.Position + (32 - 1)) & ~(32 - 1);

        //writecupiconcount
        bin.BaseStream.Position += 41; //reserved space

        long curPosition = bin.BaseStream.Position;
        bin.BaseStream.Position = sizePosition;
        bin.Write((int)(curPosition - sizePosition - 4));
        bin.BaseStream.Position = curPosition;
            */
        }

        public Result WriteCups()
        {
            cupsSection.ctsCupCount = ctsCupCount;
            cupsSection.regsMode = parameters.regsMode;

            trophyCount = new ushort[4] { 0, 0, 0, 0 };

            for (int id = 0; id < ctsCupCount; id++)
            {
                MainWindow.Cup cup = cups[id];
                Result cupRet = WriteCup(cup, false);
                if (cupRet != Result.Success) return cupRet;
            }
            if (ctsCupCount % 2 == 1)
            {
                uint idx = cups[0].idx;
                cups[0].idx = ctsCupCount;
                Result cupRet = WriteCup(cups[0], true);
                cups[0].idx = idx;
                if (cupRet != Result.Success) return cupRet;
            }
            Result regsRet = WriteRegsExperts();
            if (regsRet != Result.Success) return regsRet;
            //cupsSection.tracksArray = mainTracksList.ToArray();
            cupsSection.trophyCount = trophyCount;
            cupsSection.totalVariantCount = variantTracksList.Count;
            int totalCount = mainTracksList.Count + variantTracksList.Count;
            cupsSection.header.size = (uint)(Marshal.SizeOf(typeof(PulsarGame.CupsHolderV3)) //header
                + Marshal.SizeOf(typeof(PulsarGame.TrackV3)) * (mainTracksList.Count) //main tracks
                + (uint)Marshal.SizeOf(typeof(PulsarGame.Variant)) * (variantTracksList.Count) //variants
                + Marshal.SizeOf(typeof(ushort)) * mainTracksList.Count); //alphabetical array

            //cupsSection.header.dataSize = (uint)(Marshal.SizeOf(typeof(PulsarGame.CupV2)) * (cupList.Count - 1) + Marshal.SizeOf(typeof(PulsarGame.Cups)) + Marshal.SizeOf(typeof(ushort)) * cupList.Count * 4);

            return Result.Success;
        }

        private Result WriteCup(MainWindow.Cup cup, bool isFake)
        {
            uint cupIdx = cup.idx;

            if (!isFake)
            {
                //AppendText(cup.name);
                bmgSW.WriteLine($"  {BMGIds.BMG_CUPS + cupIdx:X}    = {cup.name}");

                string iconName = cup.iconName;
                string finalIconName = "";
                if (iconName.Length > 4 && (cupIdx >= 100 || iconName.Remove(iconName.Length - 4) != MainWindow.Cup.defaultNames[cup.idx]))
                {
                    finalIconName = iconName;
                }
                fileSW.WriteLine($"{cupIdx:X}?{finalIconName}");
            }


            for (uint i = 0; i < 4; i++)
            {
                Cup.Track curTrack = cup.tracks[i];
                Result ret = WriteMainTrack(curTrack, cupIdx * 4 + i, curTrack.expertFileNames, isFake);
                if (ret != Result.Success) return ret;
            }
            return Result.Success;
        }

        private Result WriteMainTrack(MainWindow.Cup.Track track, uint idx, string[] expertFileNames, bool isFake)
        {
            Result ret = WriteVariant(track.main, idx, 0, expertFileNames, isFake);

            if (ret != Result.Success) return ret;
            string[] empty = new string[4];
            if (track.variants.Count > 0) bmgSW.WriteLine($"  {BMGIds.BMG_TRACKS + (8 << 12) + idx:X}    = {track.commonName}");
            for (int j = 0; j < track.variants.Count; j++)
            {
                Cup.Track.Variant variant = track.variants[j];
                ret = WriteVariant(variant, idx, (uint)j + 1, empty, isFake);
                if (ret != Result.Success) return ret;
                variantTracksList.Add(new PulsarGame.Variant(variant));
            }

            //Finish setting the main track up
            uint crc32 = 0;
            string fileName = track.main.fileName;
            if (buildParams != BuildParams.ConfigOnly)
            {
                string curFile = $"input/{fileName}.szs".ToLowerInvariant(); //guaranteed to exist, has been checked by writevariant
                crc32 = BitConverter.ToUInt32(System.IO.Hashing.Crc32.Hash(File.ReadAllBytes(curFile)), 0);
            }
            if (!isFake)
            {
                string crc32Folder = "";
                if (buildParams != BuildParams.ConfigOnly)
                {
                    crcToFile.WriteLine($"{track.main.trackName} = {crc32:X8}");
                    crc32Folder = $"{modFolder}/Ghosts/{crc32:X8}".ToLowerInvariant();
                    Directory.CreateDirectory(crc32Folder);
                }

            }

            mainTracksList.Add(new PulsarGame.TrackV3(track.main, crc32, (short)track.variants.Count));

            for (int mode = 0; mode < 4; mode++) //write experts
            {
                string expertName = expertFileNames[mode];
                if (expertName != "RKG File" && expertName != "")
                {
                    if (buildParams != BuildParams.ConfigOnly)
                    {
                        string rkgName = $"input/{PulsarGame.ttModeFolders[mode, 1]}/{expertName}.rkg".ToLowerInvariant();
                        if (!inputFiles.Contains(rkgName))
                        {
                            error = $"{rkgName}";
                            return Result.FileNotFound;
                        }
                        using BigEndianReader rkg = new BigEndianReader(File.Open(rkgName, FileMode.Open));
                        rkg.BaseStream.Position = 0xC;
                        ushort halfC = rkg.ReadUInt16();
                        ushort newC = (ushort)((halfC & ~(0x7F << 2)) + (0x26 << 2)); //change ghostType to expert

                        rkg.BaseStream.Position = 0;
                        byte[] rkgBytes = rkg.ReadBytes((int)(rkg.BaseStream.Length - 4)); //-4 to remove crc32
                        using BigEndianWriter finalRkg =
                        new BigEndianWriter(File.Create($"{modFolder}/Ghosts/Experts/{idx}_{PulsarGame.ttModeFolders[mode, 0]}.rkg"));
                        rkgBytes[0xC] = (byte)(newC >> 8);
                        rkgBytes[0xD] = (byte)(newC & 0xFF);
                        finalRkg.Write(rkgBytes);
                        int rkgCrc32 = BitConverter.ToInt32(System.IO.Hashing.Crc32.Hash(rkgBytes), 0);
                        finalRkg.Write(rkgCrc32);
                    }
                    trophyCount[mode]++;
                }
            }
            return Result.Success;
        }

        private Result WriteVariant(MainWindow.Cup.Track.Variant variant, uint idx, uint variantIdx, string[] expertFileNames, bool isFake) //0 = main track
        {
            string fileName = variant.fileName;
            if (buildParams != BuildParams.ConfigOnly)
            {
                string curFile = $"input/{fileName}.szs".ToLowerInvariant();
                if (!inputFiles.Contains(curFile))
                {
                    error = $"{curFile}";
                    return Result.FileNotFound;
                }
            }
            if (!isFake)
            {
                if (buildParams != BuildParams.ConfigOnly)
                {
                    string suffix = variantIdx == 0 ? "" : $"_{variantIdx}";
                    File.Copy($"input/{fileName}.szs", $"{modFolder}/Tracks/{idx}{suffix}.szs", true);
                }

            }

            string trackName = variant.trackName;
            if (variant.versionName != "" && variant.versionName != "Version")
            {
                trackName += $" \\c{{red3}}{variant.versionName}\\c{{off}}";
            }

            bmgSW.WriteLine($"  {BMGIds.BMG_TRACKS + (variantIdx << 12) + idx:X}    = {trackName}");
            bmgSW.WriteLine($"  {BMGIds.BMG_AUTHORS + (variantIdx << 12) + idx:X}    = {variant.authorName}");
            fileSW.WriteLine($"{(variantIdx << 12) + idx:X}={variant.fileName}|" +
            $"{expertFileNames[0]}|{expertFileNames[1]}|{expertFileNames[2]}|{expertFileNames[3]}");
            return Result.Success;
        }

        public Result WriteInfo()
        {
            Random random = new Random();
            infoSection.info.roomKey = (uint)random.Next();
            infoSection.info.prob100cc = parameters.prob100cc;
            infoSection.info.prob150cc = parameters.prob150cc;
            infoSection.info.wiimmfiRegion = parameters.wiimmfiRegion;
            infoSection.info.trackBlocking = parameters.trackBlocking;
            infoSection.info.hasTTTrophies = Convert.ToByte(parameters.hasTTTrophies);
            infoSection.info.has200cc = Convert.ToByte(parameters.has200cc);
            infoSection.info.hasUMTs = Convert.ToByte(parameters.hasUMTs);
            infoSection.info.hasFeather = Convert.ToByte(parameters.hasFeather);
            infoSection.info.hasMegaTC = Convert.ToByte(parameters.hasMegaTC);
            infoSection.info.cupIconCount = Math.Min((ushort)100, ctsCupCount);
            infoSection.info.chooseNextTrackTimer = (byte)(parameters.chooseNextTrackTimer);
            infoSection.info.reservedSpace = new byte[40];

            infoSection.header.size = (uint)Marshal.SizeOf(infoSection);

            return Result.Success;
        }

        #region OldBinTypeBuild

        /*
        public Result Build()
        {
            {
                if (date == null) return Result.NoDate;
                if (parameters.modFolderName == null || parameters.modFolderName == "") return Result.NoModName;
                if (parameters.wiimmfiRegion == -1) return Result.NoWiimmfi;

                if (Directory.Exists(modFolder))
                {
                    try { Directory.Delete(modFolder, true); }
                    catch { return Result.AlreadyInUse; }
                }
                Directory.CreateDirectory("output");
                Directory.CreateDirectory(modFolder);
                Directory.CreateDirectory($"{modFolder}/Binaries");
                Directory.CreateDirectory($"{modFolder}/Tracks");
                Directory.CreateDirectory($"{modFolder}/Ghosts");
                Directory.CreateDirectory("output/Riivolution");
            }

            try
            {
                string[] bmgLines = File.ReadAllLines("temp/PulsarBMG.txt");
                string cc = parameters.has200cc ? "200" : "100";
                for (int i = 0; i < bmgLines.Length; i++)
                {
                    bmgLines[i] = bmgLines[i].Replace("{CC}", cc);
                }
                File.WriteAllLines("temp/BMG.txt", bmgLines);

                //CreateBin
                using (bin = new BigEndianWriter(File.Create("temp/Config.pul")))
                {
                    using (bmgSW = new StreamWriter("temp/BMG.txt", true))
                    using (fileSW = new StreamWriter("temp/files.txt"))
                    using (crcToFile = new StreamWriter($"{modFolder}/Ghosts/FolderToTrackName.txt"))
                    //using (textSW = new BigEndianWriter(File.Create("temp/cupText.txt"), Encoding.Unicode))
                    {
                        bmgSW.WriteLine(bmgSW.NewLine);
                        bmgSW.WriteLine($"  {0x2847:X}    = Version created {date}");
                        fileSW.WriteLine("FILE");


                        bin.Write(configMagic); //"PULS"
                        bin.Write(CONFIGVERSION);

                        //Header

                        string gameFolderName = $"/{parameters.modFolderName}";
                        bin.Write(gameFolderName.ToArray());
                        bin.BaseStream.Position += 14 - gameFolderName.Length;

                        long infoPosition = 0x40; //INFO Offset, right after header
                        bin.BaseStream.Position = 0x18;
                        bin.Write((int)infoPosition);
                        bin.BaseStream.Position = infoPosition;
                        WriteInfo();

                        long cupPosition = RoundUp(bin.BaseStream.Position, 4); //Cup Offset
                        bin.BaseStream.Position = 0x1C;
                        bin.Write((int)cupPosition);
                        bin.BaseStream.Position = cupPosition;
                        Result cupRet = WriteCups();
                        if (cupRet != Result.Success) return cupRet;
                    }


                    RequestBMGAction(true);
                    using (BigEndianReader bmgReader = new BigEndianReader(File.Open("temp/bmg.bmg", FileMode.Open)))
                    {
                        long bmgPosition = RoundUp(bin.BaseStream.Position, 4); ; //BMG Offset
                        bin.BaseStream.Position = 0x24;
                        bin.Write((int)bmgPosition);
                        bin.BaseStream.Position = bmgPosition;
                        bin.Write(bmgReader.ReadBytes((int)bmgReader.BaseStream.Length));
                    }

                    using (BigEndianReader fileReader = new BigEndianReader(File.Open("temp/files.txt", FileMode.Open)))
                    {
                        bin.Write(fileReader.ReadBytes((int)fileReader.BaseStream.Length));
                    }
                }
                File.Copy("temp/Config.pul", $"{modFolder}/Binaries/Config.pul", true);

                if (buildParams == BuildParams.ConfigAndXML || buildParams == BuildParams.Full) CreateXML();
                if (buildParams == BuildParams.Full)
                {

                    Directory.CreateDirectory($"{modFolder}/Assets");
                    Directory.CreateDirectory($"{modFolder}/CTBRSTM");
                    Directory.CreateDirectory($"{modFolder}/My Stuff");
                    File.WriteAllBytes($"{modFolder}/Binaries/Code.pul", PulsarRes.Code);
                    File.WriteAllBytes($"{modFolder}/Binaries/Loader.pul", PulsarRes.Loader);
                    File.WriteAllBytes($"{modFolder}/Assets/RaceAssets.szs", PulsarRes.RaceAssets);
                    File.WriteAllBytes($"{modFolder}/Assets/CommonAssets.szs", PulsarRes.CommonAssets);

                    bool hasCustomIcons = false;
                    Process wimgtProcess = new Process();
                    ProcessStartInfo wimgtProcessInfo = new ProcessStartInfo();
                    wimgtProcessInfo.FileName = @"temp/wimgt.exe";
                    //wimgtProcessInfo.WorkingDirectory = @"temp/";
                    wimgtProcessInfo.CreateNoWindow = true;
                    wimgtProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    wimgtProcessInfo.UseShellExecute = false;
                    wimgtProcessInfo.RedirectStandardOutput = true;
                    wimgtProcess.StartInfo = wimgtProcessInfo;

                    wimgtProcess.StandardOutput.ReadToEnd();


                    for (int i = 0; i < Math.Min(ctsCupCount, (ushort)100); i++)
                    {
                        MainWindow.Cup cup = cups[i];
                        if (cup.iconName != $"{MainWindow.Cup.defaultNames[i]}.png")
                        {
                            hasCustomIcons = true;
                            bool isDefault = MainWindow.Cup.defaultNames.Contains(cup.iconName.Remove(cup.iconName.Length - 4));
                            string realIconName = isDefault ? $"temp/{cup.iconName}" : $"input/CupIcons/{cup.iconName}";
                            if (!File.Exists(realIconName))
                            {
                                throw new Exception($"{realIconName} does not exist.");
                            }
                            using (Image image = Image.FromFile(realIconName))
                            {
                                new Bitmap(image, 128, 128).Save($"temp/{i}.png");
                            }
                            wimgtProcessInfo.Arguments = $"encode temp/{i}.png --dest temp/UIAssets.d/button/timg/icon_{i:D2}.tpl --transform CMPR -o";
                            wimgtProcess.Start();
                            wimgtProcess.WaitForExit();
                        }
                    }
                    if (hasCustomIcons)
                    {
                        ProcessStartInfo wszstProcessInfo = new ProcessStartInfo();
                        wszstProcessInfo.FileName = @"wszst.exe";
                        wszstProcessInfo.Arguments = $"create temp/UIAssets.d --dest \"{modFolder}/Assets/UIAssets.szs\" -o";
                        wszstProcessInfo.CreateNoWindow = true;
                        wszstProcessInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        wszstProcessInfo.UseShellExecute = false;
                        Process wszstProcess = new Process();
                        wszstProcess.StartInfo = wszstProcessInfo;
                        wszstProcess.Start();
                        wszstProcess.WaitForExit();
                    }
                    else File.Copy("temp/UIAssets.szs", $"{modFolder}/Assets/UIAssets.szs");
                }

            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return Result.UnknownError;
            }
            finally
            {
                File.Delete("temp/bmg.bmg");
                File.Delete("temp/bmg.txt");
                File.Delete("temp/Config.pul");
                File.Delete("temp/files.txt");
            }

            return Result.Success;

        }

        private void WriteInfo()
        {
            bin.Write(infoMagic); //INFO
            bin.Write(INFOVERSION);
            long sizePosition = bin.BaseStream.Position;
            bin.Write(0);
            Random random = new Random();
            byte[] randBytes = new byte[4];
            random.NextBytes(randBytes);
            bin.Write(BitConverter.ToUInt32(randBytes, 0));
            bin.Write(parameters.prob100cc);
            bin.Write(parameters.prob150cc);
            bin.Write(parameters.wiimmfiRegion);
            bin.Write(parameters.trackBlocking);

            bin.Write(parameters.hasTTTrophies);
            bin.Write(parameters.has200cc);
            bin.Write(parameters.hasUMTs);
            bin.Write(parameters.hasFeather);
            bin.Write(parameters.hasMegaTC);

            string test = "Test";
            byte[] a = System.Text.Encoding.Unicode.GetBytes(test);

            //reserved in case it is needed 
            //bin.BaseStream.Position = (bin.BaseStream.Position + (32 - 1)) & ~(32 - 1);

            //writecupiconcount
            bin.BaseStream.Position += 41; //reserved space

            long curPosition = bin.BaseStream.Position;
            bin.BaseStream.Position = sizePosition;
            bin.Write((int)(curPosition - sizePosition - 4));
            bin.BaseStream.Position = curPosition;
        }

        private Result WriteCups()
        {
            trophyCount = new ushort[4] { 0, 0, 0, 0 };
            bin.Write(cupsMagic); //CUPS
            bin.Write(CUPSVERSION);
            long sizePosition = bin.BaseStream.Position;
            bin.Write(0);
            bin.Write(ctsCupCount);
            bin.Write(parameters.regsMode);
            bin.BaseStream.Position += 1; //padding
            long trophyPos = bin.BaseStream.Position;
            bin.BaseStream.Position += 8;

            for (int id = 0; id < ctsCupCount; id++)
            {
                MainWindow.Cup cup = cups[id];
                Result cupRet = WriteCup(cup);
                if (cupRet != Result.Success) return cupRet;
            }
            if (ctsCupCount % 2 == 1)
            {
                uint idx = cups[0].idx;
                cups[0].idx = ctsCupCount;
                Result cupRet = WriteCup(cups[0], true);
                cups[0].idx = idx;
                if (cupRet != Result.Success) return cupRet;
            }

            WriteRegsExperts();

            long curPosition = bin.BaseStream.Position;
            bin.BaseStream.Position = trophyPos;
            for (int i = 0; i < 4; i++)
            {
                ushort count = parameters.hasTTTrophies ? trophyCount[i] : (ushort)0;
                bin.Write(count);
            }
            bin.BaseStream.Position = sizePosition;
            bin.Write((int)(curPosition - sizePosition - 4));
            bin.BaseStream.Position = curPosition;

            return Result.Success;
        }

        private Result WriteCup(MainWindow.Cup cup, bool isFake = false)
        {
            cup.idx = 0;
            uint idx = cup.idx;
            bin.Write(idx);
            if (!isFake)
            {
                //AppendText(cup.name);
                bmgSW.WriteLine($"  {0x10000 + idx:X}    = {cup.name}");

                string iconName = cup.iconName;
                string finalIconName = "";
                if (idx < 100 && iconName.Length > 4 && iconName.Remove(iconName.Length - 4) != MainWindow.Cup.defaultNames[cup.idx])
                {
                    finalIconName = iconName;
                }
                fileSW.WriteLine($"{idx:X}?{finalIconName}");
            }

            for (int i = 0; i < 4; i++)
            {
                string name = cup.fileNames[i];
                string curFile = $"input/{name}.szs".ToLowerInvariant();
                if (!inputFiles.Contains(curFile))
                {
                    error = $"{curFile}";
                    return Result.FileNotFound;
                }

                bin.Write(cup.slots[i]);
                bin.Write(cup.musicSlots[i]);
                int crc32 = BitConverter.ToInt32(System.IO.Hashing.Crc32.Hash(File.ReadAllBytes(curFile)), 0);
                bin.Write(crc32);
                if (!isFake)
                {



                    File.Copy($"input/{name}.szs", $"{modFolder}/Tracks/{idx * 4 + i}.szs", true);
                    crcToFile.WriteLine($"{name} = {crc32:X8}");
                    string crc32Folder = $"{modFolder}/Ghosts/{crc32:X8}";

                    Directory.CreateDirectory(crc32Folder);
                    for (int mode = 0; mode < 4; mode++)
                    {
                        string expertName = cup.expertFileNames[i, mode];
                        if (expertName != "RKG File" && expertName != "")
                        {
                            string rkgName = $"input/{PulsarGame.ttModeFolders[mode, 1]}\\{expertName}.rkg".ToLowerInvariant();
                            if (!inputFiles.Contains(rkgName))
                            {
                                error = $"{rkgName}";
                                return Result.FileNotFound;
                            }

                            using BigEndianReader rkg = new BigEndianReader(File.Open(rkgName, FileMode.Open));
                            rkg.BaseStream.Position = 0xC;
                            ushort halfC = rkg.ReadUInt16();
                            ushort newC = (ushort)((halfC & ~(0x7F << 2)) + (0x26 << 2)); //change ghostType to expert

                            rkg.BaseStream.Position = 0;
                            byte[] rkgBytes = rkg.ReadBytes((int)(rkg.BaseStream.Length - 4)); //-4 to remove crc32
                            Directory.CreateDirectory($"{crc32Folder}/{PulsarGame.ttModeFolders[mode, 0]}");
                            using BigEndianWriter finalRkg = new BigEndianWriter(File.Create($"{crc32Folder}/{PulsarGame.ttModeFolders[mode, 0]}/expert.rkg"));
                            rkgBytes[0xC] = (byte)(newC >> 8);
                            rkgBytes[0xD] = (byte)(newC & 0xFF);
                            finalRkg.Write(rkgBytes);
                            int rkgCrc32 = BitConverter.ToInt32(System.IO.Hashing.Crc32.Hash(rkgBytes), 0);
                            finalRkg.Write(rkgCrc32);

                            trophyCount[mode]++;
                        }
                    }

                }

                string trackName = cup.trackNames[i];
                if (cup.versionNames[i] != "" && cup.versionNames[i] != "Version")
                {
                    trackName += $" \\c{{red3}}{cup.versionNames[i]}\\c{{off}}";
                }

                //AppendText(trackName);
                //AppendText(cup.authorNames[i]);
                bmgSW.WriteLine($"  {0x20000 + idx * 4 + i:X}    = {trackName}");
                bmgSW.WriteLine($"  {0x30000 + idx * 4 + i:X}    = {cup.authorNames[i]}");
                fileSW.WriteLine($"{idx * 4 + i:X}={cup.fileNames[i]}|" +
                $"{cup.expertFileNames[i, 0]}|{cup.expertFileNames[i, 1]}|{cup.expertFileNames[i, 2]}|{cup.expertFileNames[i, 3]}");
            }
            return Result.Success;
        }
        */
        /*
        private void AppendText(string text)
        {
            (string, int) element = stringPool.Find(t => t.Item1 == text);
            if (element != default)
            {
                bin.Write(element.Item2);
            }
            else
            {
                stringPool.Add(((string, int))(text, textSW.BaseStream.Position - 2));
                bin.Write((int)textSW.BaseStream.Position - 2);
                if (text.Contains(@"\"))
                {
                    textSW.Write(text);
                }
                else
                {
                    byte[] raw =  System.Text.Encoding.Unicode.GetBytes(text);
                    byte[] utf8 = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, raw);
                    textSW.Write(utf8);
                }              
                textSW.Write(char.MinValue);
            }
        }
        */
        #endregion
        private Result WriteRegsExperts()
        {
            try
            {
                for (int cup = 0; cup < 8; cup++)
                {
                    for (int idx = 0; idx < 4; idx++)
                    {
                        for (int mode = 0; mode < 4; mode++)
                        {

                            string expertName = regsExperts[cup, idx, mode];
                            if (expertName != "RKG File" && expertName != "" && expertName != null)
                            {
                                if (buildParams != BuildParams.ConfigOnly)
                                {
                                    string rkgName = $"input/{PulsarGame.ttModeFolders[mode, 1]}/{expertName}.rkg".ToLowerInvariant();
                                    if (!inputFiles.Contains(rkgName))
                                    {
                                        error = expertName;
                                        return Result.FileNotFound;
                                    }
                                    using BigEndianReader rkg = new BigEndianReader(File.Open(rkgName, FileMode.Open));
                                    rkg.BaseStream.Position = 0xC;
                                    ushort halfC = rkg.ReadUInt16();
                                    ushort newC = (ushort)((halfC & ~(0x7F << 2)) + (0x26 << 2)); //change ghostType to expert

                                    rkg.BaseStream.Position = 0;
                                    byte[] rkgBytes = rkg.ReadBytes((int)(rkg.BaseStream.Length - 4)); //-4 to remove crc32
                                    string fileName = $"{modFolder}/Ghosts/Experts/{PulsarGame.MarioKartWii.regsGhostFolders[cup * 4 + idx]}_{PulsarGame.ttModeFolders[mode, 0]}.rkg";
                                    using BigEndianWriter finalRkg = new BigEndianWriter(File.Create(fileName));
                                    rkgBytes[0xC] = (byte)(newC >> 8);
                                    rkgBytes[0xD] = (byte)(newC & 0xFF);
                                    finalRkg.Write(rkgBytes);
                                    int rkgCrc32 = BitConverter.ToInt32(System.IO.Hashing.Crc32.Hash(rkgBytes), 0);
                                    finalRkg.Write(rkgCrc32);
                                }
                                trophyCount[mode]++;
                            }
                        }
                        fileSW.WriteLine($"{cup * 4 + idx:X}=" +
                            $"{regsExperts[cup, idx, 0]}*{regsExperts[cup, idx, 1]}*{regsExperts[cup, idx, 2]}*{regsExperts[cup, idx, 3]}");
                    }

                }
                return Result.Success;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return Result.UnknownError;
            }
        }

        private void CreateXML()
        {
            char[] delims = new[] { '\r', '\n' };
            string[] xml = PulsarRes.XML.Split(delims, StringSplitOptions.RemoveEmptyEntries);

            // ランダムID生成（LoadPackなどに使う）
            uint rand = (uint)new Random().Next(1 << 8);
            string randomName = $"{parameters.modFolderName}{rand}";

            for (int i = 0; i < xml.Length; i++)
            {
                // 動的なID置換：patch id に "LoadPack", "Load" が含まれている場合はランダム付き
                if (xml[i].Contains("patch id=\"{$pack}LoadPack\"") ||
                    xml[i].Contains("patch id=\"{$pack}Load\"") ||
                    xml[i].Contains("<patch id=\"{$pack}LoadPack\"") ||
                    xml[i].Contains("<patch id=\"{$pack}Load\""))
                {
                    xml[i] = xml[i].Replace("{$pack}", randomName);
                }
                else
                {
                    xml[i] = xml[i].Replace("{$pack}", parameters.modFolderName);
                }
            }

            File.WriteAllLines($"output/Riivolution/{parameters.modFolderName}.xml", xml);

            /*
            char[] delims = new[] { '\r', '\n' };
            string[] xml = PulsarRes.XML.Split(delims, StringSplitOptions.RemoveEmptyEntries);

            xml[3] = xml[3].Replace("{$pack}", parameters.modFolderName);
            xml[28] = xml[28].Replace("{$pack}", parameters.modFolderName);
            xml[29] = xml[29].Replace("{$pack}", parameters.modFolderName);
            xml[30] = xml[30].Replace("{$pack}", parameters.modFolderName);
            xml[31] = xml[31].Replace("{$pack}", parameters.modFolderName);
            xml[32] = xml[32].Replace("{$pack}", parameters.modFolderName);
            xml[33] = xml[33].Replace("{$pack}", parameters.modFolderName);
            xml[34] = xml[34].Replace("{$pack}", parameters.modFolderName);
            xml[35] = xml[35].Replace("{$pack}", parameters.modFolderName);
            xml[36] = xml[36].Replace("{$pack}", parameters.modFolderName);
            xml[37] = xml[37].Replace("{$pack}", parameters.modFolderName);
            xml[38] = xml[38].Replace("{$pack}", parameters.modFolderName);
            xml[39] = xml[39].Replace("{$pack}", parameters.modFolderName);
            xml[40] = xml[40].Replace("{$pack}", parameters.modFolderName);
            xml[41] = xml[41].Replace("{$pack}", parameters.modFolderName);
            xml[42] = xml[42].Replace("{$pack}", parameters.modFolderName);
            xml[43] = xml[43].Replace("{$pack}", parameters.modFolderName);
            xml[44] = xml[44].Replace("{$pack}", parameters.modFolderName);
            xml[45] = xml[45].Replace("{$pack}", parameters.modFolderName);

            xml[54] = xml[54].Replace("{$pack}", parameters.modFolderName);
            xml[55] = xml[55].Replace("{$pack}", parameters.modFolderName);
            xml[56] = xml[56].Replace("{$pack}", parameters.modFolderName);
            xml[57] = xml[57].Replace("{$pack}", parameters.modFolderName);

            uint rand = (uint)new Random().Next(1 << 8); ;
            xml[6] = xml[6].Replace("{$pack}", $"{parameters.modFolderName}{rand}");
            xml[14] = xml[14].Replace("{$pack}", $"{parameters.modFolderName}{rand}");
            xml[19] = xml[19].Replace("{$pack}", $"{parameters.modFolderName}{rand}");
            xml[53] = xml[53].Replace("{$pack}", $"{parameters.modFolderName}{rand}");
            File.WriteAllLines($"output/Riivolution/{parameters.modFolderName}.xml", xml);
            */
        }

    }



}
