﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapRender.Invoker;

namespace MapleStory.Sampler
{
    public class DarknetWriter : IDatasetWriter, IDisposable
    {
        private const string DefaultRootDirectory = "data";
        private const string ClassNamesFile = "obj.names";
        private const string ObjectDataFile = "obj.data";
        private const string TrainingDataFile = "train.txt";
        private const string TestingDataFile = "test.txt";
        private const string ObjDirectory = "obj";

        private List<ObjectClass> _occurenceClasses;
        private bool _isFinished;
        private StreamWriter _trainingDataListWriter;

        public string RootPath { get; private set; }

        public string ObjPath { get; private set; }

        public DarknetWriter(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Target path {path} does not exist. Creating one...");
                Directory.CreateDirectory(path);
            }
            RootPath = Path.Combine(path, DefaultRootDirectory);
            if (!Directory.Exists(RootPath))
            {
                Directory.CreateDirectory(RootPath);
            }
            CleanDirectory(RootPath);
            ObjPath = Path.Combine(RootPath, ObjDirectory);
            Directory.CreateDirectory(ObjPath);
            _occurenceClasses = new List<ObjectClass>();
            _isFinished = false;
            _trainingDataListWriter = new StreamWriter(new FileStream(Path.Combine(RootPath, TrainingDataFile), FileMode.CreateNew));
        }


        public void Write(Sample sample)
        {
            // Write training list
            string trainDataLine = $"{DefaultRootDirectory}/{ObjDirectory}/{sample.Guid + ".jpg"}";
            _trainingDataListWriter.WriteLine(trainDataLine);

            // Write images
            using (FileStream imageStream =
                new FileStream(Path.Combine(ObjPath, sample.Guid + ".jpg"), FileMode.CreateNew))
            {
                sample.ImageStream.WriteTo(imageStream);
            }
            // Write labels
            using (FileStream lableStream =
                new FileStream(Path.Combine(ObjPath, sample.Guid + ".txt"), FileMode.CreateNew))
            {
                using (StreamWriter lableStreamWriter = new StreamWriter(lableStream))
                {
                    WriteItems(sample, lableStreamWriter);
                }
            }
        }

        public void Finish()
        {
            WriteObjData();
            WriteClassNames();
            _trainingDataListWriter.Flush();
            _isFinished = true;
        }

        /// <summary>
        /// Write to imageName.txt
        /// </summary>
        /// <seealso cref="https://github.com/AlexeyAB/darknet#how-to-train-with-multi-gpu"/>
        private void WriteItems(Sample sample, StreamWriter writer)
        {
            double width = sample.Width;
            double height = sample.Height;
            foreach (var sampleItem in sample.Items)
            {
                ObjectClass type = sampleItem.Type;
                if (!_occurenceClasses.Contains(type))
                {
                    _occurenceClasses.Add(type);
                }

                double xCenter = ((double)sampleItem.X + sampleItem.Width / 2.0) / width;
                double yCenter = ((double)sampleItem.Y + sampleItem.Height / 2.0) / height;
                double sampleWidth = sampleItem.Width / width;
                double sampleHeight = sampleItem.Height / height;

                writer.Write(_occurenceClasses.IndexOf(type)); // <object-class>
                writer.Write(' ');
                writer.Write(xCenter); // <x_center>
                writer.Write(' ');
                writer.Write(yCenter); // <y_center>
                writer.Write(' ');
                writer.Write(sampleWidth); // <width>
                writer.Write(' ');
                writer.Write(sampleHeight); // <height>
                writer.WriteLine();
            }
        }

        private void CleanDirectory(string path)
        {

            if (Directory.EnumerateFileSystemEntries(path).Any())
            {
                Console.WriteLine("Warning: Target RootPath Is Not Empty. Cleaning target path...");
                DirectoryInfo directory = new DirectoryInfo(path);
                foreach (var fileInfo in directory.GetFiles())
                {
                    fileInfo.Delete();
                }
                foreach (var directoryInfo in directory.GetDirectories())
                {
                    directoryInfo.Delete(true);
                }
            }
        }

        private void WriteObjData()
        {
            using (FileStream file = new FileStream(Path.Combine(RootPath, ObjectDataFile), FileMode.CreateNew))
            {
                using (StreamWriter writer = new StreamWriter(file))
                {
                    writer.WriteLine($"classes={_occurenceClasses.Count}");
                    writer.WriteLine($"train={DefaultRootDirectory}/{TrainingDataFile}");
                    writer.WriteLine($"valid={DefaultRootDirectory}/{TestingDataFile}");
                    writer.WriteLine($"names={DefaultRootDirectory}/{ClassNamesFile}");
                    writer.WriteLine(@"backup = backup/");
                }
            }
        }

        private void WriteClassNames()
        {
            using (FileStream file = new FileStream(Path.Combine(RootPath, ClassNamesFile), FileMode.CreateNew))
            {
                using (StreamWriter writer = new StreamWriter(file))
                {
                    foreach (var occurenceClass in _occurenceClasses)
                    {
                        writer.WriteLine(Enum.GetName(typeof(ObjectClass), occurenceClass));
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_isFinished)
            {
                Finish();
            }
            _trainingDataListWriter.Dispose();
            _trainingDataListWriter.BaseStream.Dispose();
        }

        ~DarknetWriter()
        {
            Dispose();
        }

    }
}
