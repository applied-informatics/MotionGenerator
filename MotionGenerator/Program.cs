using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MotionGenerator
{
    class Node
    {
        public string NodeId { get; set; }
        public long X { get; set; }
        public long Y { get; set; }
        public long XSpeed { get; set; }
        public long YSpeed { get; set; }
        public long LinearSpeed { get; set; }

        public void Move(long width, long height, Random r)
        {
            if (MovingTo == null)   // brownian motion
            {
                if (X + XSpeed > width || X + XSpeed < 0) XSpeed *= -1;
                if (Y + YSpeed > Y || YSpeed < 0) YSpeed *= -1;

                X += XSpeed;
                Y += YSpeed;

                if (r.NextDouble() < 0.01)
                {
                    XSpeed = r.Next(20) - 10;
                    YSpeed = r.Next(20) - 10;
                }
            }
            else    // transport graph motion
            {
                long xOffset = MovingTo.X - X;
                long yOffset = MovingTo.Y - Y;

                double distance = Math.Sqrt(xOffset * xOffset + yOffset * yOffset);
                double relative = ((double)LinearSpeed) / distance;
                xOffset = (long) Math.Round(xOffset * relative);
                yOffset = (long) Math.Round(yOffset * relative);

                X += xOffset;
                Y += yOffset;
            }
        }

        public List<Node> ConnectedNodes { get; set; }
        public Node MovingTo { get; set; }

        public Node()
        {
            ConnectedNodes = new List<Node>();
        }
    }

    class Field
    {
        public long Width { get; set; }
        public long Height { get; set; }
        public List<Node> Nodes { get; private set; } 

        public Field(long width, long height)
        {
            Width = width;
            Height = height;
            Nodes = new List<Node>();
        }

        public void Move(Random r)
        {
            Nodes.ForEach(n => n.Move(Width, Height, r));
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            var lowercaseArgs = args.Select(a => a.ToLowerInvariant()).ToArray();
            if (args.Length == 0 || lowercaseArgs[0].ToLowerInvariant() == "help")
            {
                Help();
                return;
            }

            if (lowercaseArgs[0] == "brownian")
            {
                if (args.Length < 6)
                {
                    Help();
                    return;
                }

                uint width;
                uint height;
                uint nodeCount;
                uint generationCount;
                string outputFileName = lowercaseArgs[5];

                if (!uint.TryParse(lowercaseArgs[1], out width)
                    || !uint.TryParse(lowercaseArgs[2], out height)
                    || !uint.TryParse(lowercaseArgs[3], out nodeCount)
                    || !uint.TryParse(lowercaseArgs[4], out generationCount))
                {
                    Help();
                    return;
                }

                Brownian((int)width, (int)height, (int)nodeCount, (int)generationCount, outputFileName);

            }

            if (lowercaseArgs[0] == "transport")
            {
                if (args.Length < 5)
                {
                    Help();
                    return;
                }

                uint generationCount;
                uint nodeCount;

                if (!uint.TryParse(lowercaseArgs[4], out generationCount)
                    || !uint.TryParse(lowercaseArgs[3], out nodeCount)
                    || !File.Exists(lowercaseArgs[1]))
                {
                    Help();
                    return;
                }

                Transport(lowercaseArgs[1], lowercaseArgs[2], (int)nodeCount, (int)generationCount);
            }
        }

        private static void Help()
        {
            Console.WriteLine("MotionGenerator v0.01b");
            Console.WriteLine("Usage: MotionGenerator [режим] <параметры>");
            Console.WriteLine("help\tВывести Этот текст");
            Console.WriteLine("brownian [width] [height] [nodeCount] [generationCount] [outputFileName]" +
                              "\tГенерация броуновского движения. \n" +
                              "\t\twidth - ширина поля\n" +
                              "\t\theight - высота поля\n" +
                              "\t\tnodeCount - количество узлов\n" +
                              "\t\tgenerationCount - количество временных отсчётов\n" +
                              "\t\toutputFileName - имя выходного xml-файла");

            Console.WriteLine("transport [inputFileName] [outputFileName] [nodeCount] [generationCount]" +
                              "\tГенерация движения по транспортному графу. \n" +
                              "\t\tinputFileName - имя входного xml-файла с транспортным графом\n" +
                              "\t\toutputFileName - имя выходного xml-файла" + 
                              "\t\tnodeCount - количество узлов\n" +
                              "\t\tgenerationCount - количество временных отсчётов\n"
            );
        }

        private static void Brownian(int width, int height, int nodeCount, int generationCount, string outputFileName)
        {
            var field = new Field(width, height);
            var r = new Random();
            for (long i = 0; i < nodeCount; i++)
            {
                field.Nodes.Add(new Node
                    {
                        NodeId = "n" + (i+1),
                        X = r.Next(width),
                        Y = r.Next(height),
                        XSpeed = r.Next(20) - 10,
                        YSpeed = r.Next(20) - 10
                    });
            }

            var output = new XDocument();
            var fieldElement = new XElement("field",
                new XAttribute("width", width),
                new XAttribute("height", height),
                new XAttribute("generationCount", generationCount)
            );
            for (int g = 0; g < generationCount; g++)
            {
                var genElement = new XElement("gen", 
                    new XAttribute("n", g+1)
                );

                foreach (var n in field.Nodes)
                {
                    genElement.Add(new XElement("node",
                        new XAttribute("id", n.NodeId),
                        new XAttribute("x", n.X),
                        new XAttribute("y", n.Y)
                        )
                    );
                }

                Func<Node, Node, double> dist = (n1, n2) => {
                    return Math.Sqrt(Math.Pow(n1.X - n2.X, 2) + Math.Pow(n1.Y - n2.Y, 2));
                };

                int idCounter = 0;

                foreach (var n1 in field.Nodes)
                {
                    foreach (var n2 in field.Nodes)
                    {
                        if (n1.NodeId == n2.NodeId)
                            continue;

                        if (dist(n1, n2) <= 75) 
                        {
                            genElement.Add(new XElement("edge",
                                new XAttribute("id", string.Format("e{0}", idCounter++)),
                                new XAttribute("from", n1.NodeId),
                                new XAttribute("to", n2.NodeId)
                                )
                            );
                        }
                    }
                }

                fieldElement.Add(genElement);
                field.Move(r);

            }

            output.Add(fieldElement);
            using (var writer = new XmlTextWriter(outputFileName, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                output.WriteTo(writer);
                writer.Flush();
            }
        }

        private static void Transport(string inputGraphFilename, string outputFileName, int nodeCount, int generationCount)
        {
            var r = new Random();
            var inputGraphXml = XDocument.Load(inputGraphFilename);
            var inputFieldElement = inputGraphXml.Element("field");
            var field = new Field(long.Parse(inputFieldElement.Attribute("width").Value),
                                  long.Parse(inputFieldElement.Attribute("height").Value));

            var transportNodes = inputFieldElement.Descendants("node")
                    .Select(n => new Node { 
                        X = long.Parse(n.Attribute("x").Value),
                        Y = long.Parse(n.Attribute("y").Value),
                        XSpeed = 0,
                        YSpeed = 0,
                        NodeId = n.Attribute("id").Value
                    })
                    .ToList();

            foreach (var edge in inputFieldElement.Descendants("edge"))
            {
                var from = transportNodes.FirstOrDefault(n => n.NodeId == edge.Attribute("from").Value);
                var to = transportNodes.FirstOrDefault(n => n.NodeId == edge.Attribute("to").Value);

                if (from != null && to != null)
                {
                    from.ConnectedNodes.Add(to);
                }
            }

            const long nodeReachDistance = 20;

            for (int i = 1; i <= nodeCount; i++)
            {
                var transportNode = transportNodes[r.Next(transportNodes.Count)];

                var node = new Node
                {
                    NodeId = "n" + i,
                    X = transportNode.X,
                    Y = transportNode.Y,
                    MovingTo = transportNode.ConnectedNodes[r.Next(transportNode.ConnectedNodes.Count)],
                    LinearSpeed = r.Next(19) + 1
                };

                field.Nodes.Add(node);
            }


            Func<Node, Node, double> dist = (n1, n2) => {
                    return Math.Sqrt(Math.Pow(n1.X - n2.X, 2) + Math.Pow(n1.Y - n2.Y, 2));
            };

            var output = new XDocument();
            var fieldElement = new XElement("field",
                new XAttribute("width", field.Width),
                new XAttribute("height", field.Height),
                new XAttribute("generationCount", generationCount)
            );

            for (int g = 0; g < generationCount; g++)
            {
                foreach (var node in field.Nodes)
                {
                    while (dist(node, node.MovingTo) <= nodeReachDistance)
                    {
                        node.MovingTo = node.MovingTo.ConnectedNodes[r.Next(node.MovingTo.ConnectedNodes.Count)];
                    }
                }

                var genElement = new XElement("gen",
                    new XAttribute("n", g + 1)
                );

                foreach (var n in field.Nodes)
                {
                    genElement.Add(new XElement("node",
                        new XAttribute("id", n.NodeId),
                        new XAttribute("x", n.X),
                        new XAttribute("y", n.Y)
                        )
                    );
                }

                int idCounter = 0;

                foreach (var n1 in field.Nodes)
                {
                    foreach (var n2 in field.Nodes)
                    {
                        if (n1.NodeId == n2.NodeId)
                            continue;

                        if (dist(n1, n2) <= 75)
                        {
                            genElement.Add(new XElement("edge",
                                new XAttribute("id", string.Format("e{0}", idCounter++)),
                                new XAttribute("from", n1.NodeId),
                                new XAttribute("to", n2.NodeId)
                                )
                            );
                        }
                    }
                }

                fieldElement.Add(genElement);
                field.Move(r);
            }


            output.Add(fieldElement);
            using (var writer = new XmlTextWriter(outputFileName, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                output.WriteTo(writer);
                writer.Flush();
            }
        }
    }
}
