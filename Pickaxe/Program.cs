﻿/* Copyright 2015 Brock Reeve
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using log4net;
using Pickaxe.Runtime;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pickaxe.Emit;

namespace Pickaxe
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Main(string[] args)
        {
            string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string log4netPath = Path.Combine(Path.GetDirectoryName(location), "Log4net.config");
            log4net.Config.XmlConfigurator.Configure(new FileInfo(log4netPath));

            if (args.Length == 0) //interactive prompt
                Interactive();
            else
            { //run the file
                var reader = new StreamReader(args[0]);
                string source = reader.ReadToEnd();

                Thread thread = new Thread(() => Compile(source));
                thread.Start();
                thread.Join();
            }
        }

        private static void ListErrors(string[] errors)
        {
            foreach (var error in errors)
                Console.WriteLine(error);
        }

        private static void Compile(string source)
        {
            Log.Info("Compiling...");

            var compiler = new Compiler(source);
            var generatedAssembly = compiler.ToAssembly();

            if (compiler.Errors.Any())
                ListErrors(compiler.Errors.Select(x => x.Message).ToArray());

            if (!compiler.Errors.Any())
            {
                var runable = new Runable(generatedAssembly);
                runable.Select += OnSelectResults;
                //runable.Progress += OnProgress;

                try
                {
                    Log.Info("Running...");
                    runable.Run();
                }
                catch (ThreadAbortException)
                {
                    Log.Info("Program aborted");
                }
                catch (Exception e)
                {
                    Log.Fatal("Unexpected Exception", e);
                }
            }
        }

        private static void Interactive()
        {
            //interactive prompt ; delimited.
            var builder = new StringBuilder();
            Console.Write("pickaxe> ");
            while (true)
            {
                char character = Convert.ToChar(Console.Read());

                if (character == '\n')
                    Console.Write("      -> ");
                if (character == ';') //run it. 
                {
                    while (Convert.ToChar(Console.Read()) != '\n') {} //clear buf

                    Thread thread = new Thread(() => Compile(builder.ToString()));
                    thread.Start();
                    thread.Join();

                    builder.Clear();
                    Console.Write("pickaxe> ");
                    continue;
                }

                builder.Append(character);
            }
        }

        private static List<int> Measure(RuntimeTable<ResultRow> result)
        {
            var lengths = new List<int>();

            foreach (var column in result.Columns()) //headers
                lengths.Add(column.Length + 2);

            for (int row = 0; row < result.RowCount; row++)
            {
                for (int col = 0; col < lengths.Count; col++)
                {
                    int len = result[row][col].ToString().Length + 2;
                    if (len > lengths[col])
                        lengths[col] = len;
                }
            }

            return lengths;
        }

        private static string Border(List<int> lengths)
        {
            var topBottom = new StringBuilder();
            for (int x = 0; x < lengths.Count; x++)
            {
                topBottom.Append("+");
                for (int len = 0; len < lengths[x]; len++)
                    topBottom.Append("-");
            }
            topBottom.Append("+");
            return topBottom.ToString();
        }

        private static string Values(List<int> lengths, string[] values)
        {
            var middle = new StringBuilder();
            var columns = values;
            for (int x = 0; x < lengths.Count; x++)
            {
                middle.Append("|");
                int totalPadding = (lengths[x] - columns[x].Length);
                int leftPadding = 1;
                int righPaddding = totalPadding - leftPadding;
                for (int pad = 0; pad < leftPadding; pad++)
                    middle.Append(" ");

                middle.Append(string.Format("{0}", columns[x]));
                for (int pad = 0; pad < righPaddding; pad++)
                    middle.Append(" ");
            }
            middle.Append("|");

            return middle.ToString();
        }

        private static void OnSelectResults(RuntimeTable<ResultRow> result)
        {
            var lengths = Measure(result);
            
            //+--+-------------------+------------+               
            //|  |  (No column name) | .content a |
            //+--+-------------------+------------+

            var border = Border(lengths);
            Console.WriteLine(border);
            var values = Values(lengths, result.Columns());
            Console.WriteLine(values.ToString());
            Console.WriteLine(border.ToString());

            for (int row = 0; row < result.RowCount; row++)
            {
                var valueList = new List<string>();
                for (int col = 0; col < lengths.Count; col++)
                    valueList.Add(result[row][col].ToString());

                Console.WriteLine(Values(lengths, valueList.ToArray()));
            }
            Console.WriteLine(border.ToString());
        }
    }
}
