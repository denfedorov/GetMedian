using System;
using System.Net.Sockets;
using System.IO;
using System.Text.Encodings;
using System.Threading;
using System.Collections.Generic;

namespace GetMedian
{
        class Program
        {
            static void Main(string[] args)
            {
                Console.WriteLine("StartedFetching");
                using (StreamWriter writer = new StreamWriter("./result.txt"))
                {
                    writer.WriteLine("Started {0}", DateTime.Now);
                }
                Algo a = new Algo();
                a.FetchVals();
            }

        }

        class Algo
        {
            public const int valscount = 2018;
            public const int maxthreads = 150;
            public const string server = "88.212.241.115";
            public const Int32 port = 2013;
            public const string server_encoding = "koi8-r";

            public enum ThreadState : byte
            {
                free = 1,
                calculated = 2,
                pending = 3,
                error = 4
            }

            public class THreadValue
            {
                public UInt64 value;
                public ThreadState state;

                public THreadValue()
                {
                    value = 0;
                    state = ThreadState.free;
                }
            }

            public Algo()
            {
                threadvals = new THreadValue[valscount];
                for (int i = 0; i < valscount; i++)
                {
                    threadvals[i] = new THreadValue();
                }
            }

            public static THreadValue[] threadvals;

            public static bool AllCalculated()
            {
                foreach (THreadValue tv in threadvals)
                {
                    if (tv.state != ThreadState.calculated)
                    {
                        return false;
                    }
                }
                return true;
            }

            public static double Progress()
            {
                double c = 0;
                for (int i = 0; i < valscount; i++)
                {
                    if (threadvals[i].state == ThreadState.calculated)
                    {
                        c += 1;
                    }
                }
                return 100.0 * (c / valscount);
            }

            public static double getMedian()
            {
                double t = 0;
                UInt64[] a = new UInt64[valscount];
                for (int i = 0; i < valscount; i++)
                {
                    a[i] = threadvals[i].value;
                }
                Array.Sort(a);
                using (StreamWriter writer = File.AppendText("./result.txt"))
                {
                    for (int i = 0; i < valscount; i++)
                    {
                        writer.WriteLine("{0}:{1}", i, a[i]);
                    }
                }
                if (valscount % 2 == 0)
                {
                    return (a[valscount / 2] + a[valscount / 2 - 1]) / 2.0;
                }

#pragma warning disable CS0162 // Обнаружен недостижимый код
                return (double)(a[valscount / 2] * 1.0);
#pragma warning restore CS0162 // Обнаружен недостижимый код

            }

            public static int FindFreeTHreadVal()
            {
                for (int index = 0; index < valscount; index++)
                {
                    if (threadvals[index].state == ThreadState.free)
                    {
                        return index;
                    }
                }
                return -1;
            }



            class MyThread
            {
                public int index;
                Thread thread;


                public MyThread(int valindex) //Конструктор получает  номер до кторого ведется счет
                {

                    index = valindex;
                    thread = new Thread(getval);
                    thread.Start(valindex);
                }

                static UInt64 SearchVal(ref string s, ref bool err)
                {
                    int start = 0, stop = s.Length - 1;
                    err = false;
                    while (start < s.Length)
                    {
                        if (s[start] >= '0' && s[start] <= '9')
                        {
                            break;
                        }
                        start++;
                    }
                    while (stop >= 0)
                    {
                        if (s[stop] >= '0' && s[stop] <= '9')
                        {
                            break;
                        }
                        stop--;
                    }

                    if (stop - start < 0 || !(s[start] >= '0' && s[start] <= '9'))
                    {
                        err = true;
                        return 0;
                    }

                    String ts = s.Substring(start, stop - start + 1);
                    UInt64 dt = 0;
                    try
                    {
                        dt = Convert.ToUInt64(ts);
                        if (dt < 0 || dt >= 1e7)
                        {
                            Console.WriteLine("Error: invalid UInt64 s: {0}", dt);
                            err = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: to UInt64 s: {0}", ts);
                        err = true;
                        return 0;
                    }
                    return dt;
                }

                static bool CheckDigits(ref Byte[] resp, int bytesread)
                {
                    for (int i = 0; i < bytesread; i++)
                    {
                        if (resp[i] >= 48 && resp[i] <= 57)
                        {
                            return true;
                        }
                    }
                    return false;
                }



                static void getval(object o)//Функция потока, передаем параметр
                {
                    int index = (int)o;

                    if (threadvals[index].state == ThreadState.free)
                    {

                        do
                        {
                            threadvals[index].state = ThreadState.pending;
                            //Console.WriteLine("Fetching value number: {0}", index + 1);
                            try
                            {
                                TcpClient client = new TcpClient(server, port);

                                string query = (index + 1).ToString() + "\n";
                                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                                System.Text.Encoding serverenc = System.Text.Encoding.GetEncoding(server_encoding);


                                Byte[] data = serverenc.GetBytes(query);

                                NetworkStream stream = client.GetStream();
                                stream.ReadTimeout = 2500;

                                stream.Write(data, 0, data.Length);

                                byte[] resp = new byte[256];
                                var memStream = new MemoryStream();
                                int bytesread = 0;

                                bool readerror = true;
                                for (int i = 0; i < 10; i++)
                                {
                                    Thread.Sleep(300);
                                    bytesread = stream.Read(resp, 0, resp.Length);
                                    if (CheckDigits(ref resp, bytesread))
                                    {
                                        memStream.Write(resp, 0, bytesread);
                                    }
                                    if (bytesread > 0)
                                    {
                                        i = 0;
                                        if (resp[bytesread - 1] == 10)
                                        {
                                            readerror = false;
                                            break;
                                        }
                                    }

                                };

                                client.Close();

                                String responseData = String.Empty;

                                responseData = System.Text.Encoding.ASCII.GetString(memStream.ToArray());
                                if (responseData.Length > 0 && !readerror)
                                {
                                    //Console.WriteLine("Raw: {0}", responseData);

                                    bool error = false;
                                    UInt64 t = SearchVal(ref responseData, ref error);

                                    if (error)
                                    {
                                        threadvals[index].state = ThreadState.error;
                                    }
                                    else
                                    {
                                        threadvals[index].value = t;
                                        Console.WriteLine("Fetched value [index] val: [{0}] {1}", index.ToString(), t.ToString());
                                        threadvals[index].state = ThreadState.calculated;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                //   Console.WriteLine(e.Message);
                                threadvals[index].state = ThreadState.error;

                            }

                            Thread.Sleep(20);
                            //Console.WriteLine("Refetch value by index {0}", index + 1);
                        } while (threadvals[index].state != ThreadState.calculated);
                    }
                    //Console.WriteLine("Fetched value by number {0} is {1}", index + , threadvals[index].value);
                }
            }


            public void FetchVals()
            {
                List<MyThread> th_list = new List<MyThread>();
                th_list.Clear();
                do
                {
                    //Try fetch vals
                    if (th_list.Count < maxthreads)
                    {
                        int nth = FindFreeTHreadVal();
                        if (nth > -1)
                        {

                            th_list.Add(new MyThread(nth));
                            Console.WriteLine("Started fetching value by index {0}, threads pending {1} of {2}, progress: {3}", nth, th_list.Count, maxthreads, Progress());
                            Thread.Sleep(500);
                        }
                    }

                    //delete error results
                    //foreach (MyThread th in th_list.FindAll(item => threadvals[item.index].state == ThreadState.error))
                    //{
                    //    th_list.Remove(th);
                    //   threadvals[th.index].state = ThreadState.free;

                    //}
                    //delete successe threads
                    foreach (MyThread th in th_list.FindAll(item => threadvals[item.index].state == ThreadState.calculated))
                    {
                        th_list.Remove(th);
                    }

                } while (!AllCalculated());

                double m = getMedian();
                Console.WriteLine("Calculated median: {0}", m.ToString());
                using (StreamWriter writer = File.AppendText("./result.txt"))
                {
                    writer.WriteLine("Calculated median: {0}", m);
                }
            }

        }

}
