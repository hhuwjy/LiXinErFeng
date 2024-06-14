using Arp.Plc.Gds.Services.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using HslCommunication.LogNet;
using HslCommunication.Profinet.Omron;
using HslCommunication;
using NPOI.XSSF.UserModel;
using Ph_Mc_LiXinErFeng;
using static Arp.Plc.Gds.Services.Grpc.IDataAccessService;
using static Ph_Mc_LiXinErFeng.GrpcTool;
using static Ph_Mc_LiXinErFeng.UserStruct;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using HslCommunication.Profinet.Keyence;
using Opc.Ua;
using NPOI.Util;

namespace Ph_Mc_LiXinErFeng
{
    class Program
    {
        /// <summary>
        /// app初始化
        /// </summary>

        // 创建日志
        const string logsFile = ("/opt/plcnext/apps/LiXinErFengAppLogs1.txt");
        //const string logsFile = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\LiXinErFengAppLogs.txt";
        public static ILogNet logNet = new LogNetFileSize(logsFile, 5 * 1024 * 1024); //限制了日志大小

        //创建Grpc实例
        public static GrpcTool grpcToolInstance = new GrpcTool();

        //设置grpc通讯参数
        public static CallOptions options1 = new CallOptions(
                new Metadata {
                        new Metadata.Entry("host","SladeHost")
                },
                DateTime.MaxValue,
                new CancellationTokenSource().Token);
        public static IDataAccessServiceClient grpcDataAccessServiceClient = null;

        //创建ASCII 转换API实例
        public static ToolAPI tool = new ToolAPI();

        //MC Client实例化 
        public static KeyenceComm keyenceClients = new KeyenceComm();
        static int clientNum = 2;  //一个EPC对应采集两个基恩士的数据（点表相同）  上位链路+MC协议，同时在线加起来不能超过15台
        public static KeyenceMcNet[] _mc = new KeyenceMcNet[clientNum];

        //创建三个线程            
        static int thrNum = 5;  //开启三个线程
        static Thread[] thr = new Thread[thrNum];

        //创建nodeID字典 (读取XML用）
        public static Dictionary<string, string> nodeidDictionary1;
        public static Dictionary<string, string> nodeidDictionary2;


        //读取Excel用
        static ReadExcel readExcel = new ReadExcel();

        #region 从Excel解析来的数据实例化 (4795 4794 )

        //设备信息数据
        static DeviceInfoConSturct_MC[] StationMemory_LXEF1;

        //两个工位数据
        static StationInfoStruct_MC[] StationData1_LXEF1;
        static StationInfoStruct_MC[] StationData2_LXEF1;

        //1000ms 非报警信号
        static OneSecInfoStruct_MC[] OEE1_LXEF1;
        static OneSecInfoStruct_MC[] OEE2_LXEF1;
        static OneSecInfoStruct_MC[] Production_Data_LXEF1;
        static OneSecInfoStruct_MC[] Function_Enable_LXEF1;
        static OneSecInfoStruct_MC[] Life_Management_LXEF1;

        //报警信号
        static OneSecAlarmStruct_MC[] Alarm_LXEF1;

        #endregion

  

     
        #region 数据点位名和设备总览表格的实例化结构体

        //点位名
        static OneSecPointNameStruct_IEC PointNameStruct_IEC = new OneSecPointNameStruct_IEC();

        // 设备总览
        static DeviceInfoStruct_IEC[] deviceInfoStruct1_IEC;    //LXEFData(4795 4794 4785)
 
        #endregion


        static void Main(string[] args)
        {

            int stepNumber = 5;


            List<WriteItem> listWriteItem = new List<WriteItem>();
            IDataAccessServiceReadSingleRequest dataAccessServiceReadSingleRequest = new IDataAccessServiceReadSingleRequest();

            bool isThreadZeroRunning = false;
            bool isThreadOneRunning = false;
            bool isThreadTwoRunning = false;
            bool isThreadThreeRunning = false;


            int IecTriggersNumber = 0;

            //采集值缓存区，需要写入Excel
            AllDataReadfromMC allDataReadfromMC_4795 = new AllDataReadfromMC();
            AllDataReadfromMC allDataReadfromMC_4794 = new AllDataReadfromMC();


            while (true)
            {
                switch (stepNumber)
                {

                    case 5:
                        {
                            #region Grpc连接

                            var udsEndPoint = new UnixDomainSocketEndPoint("/run/plcnext/grpc.sock");
                            var connectionFactory = new UnixDomainSocketConnectionFactory(udsEndPoint);

                            //grpcDataAccessServiceClient
                            var socketsHttpHandler = new SocketsHttpHandler
                            {
                                ConnectCallback = connectionFactory.ConnectAsync
                            };
                            try
                            {
                                GrpcChannel channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions  // Create a gRPC channel to the PLCnext unix socket
                                {
                                    HttpHandler = socketsHttpHandler
                                });
                                grpcDataAccessServiceClient = new IDataAccessService.IDataAccessServiceClient(channel);// Create a gRPC client for the Data Access Service on that channel
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("ERRO: {0}", e);
                                //logNet.WriteError("Grpc connect failed!");
                            }
                            #endregion
                        
                            stepNumber = 6;

                        }
                        break;


                case 6:
                       {

  
                            #region 从xml获取nodeid，Grpc发送到对应变量时使用，注意xml中的别名要和对应类的属性名一致 

                            //4795
                            try
                            {
                                //EPC中存放的路径
                                const string filePath1 = "/opt/plcnext/apps/GrpcSubscribeNodes_4795.xml";

                                //PC中存放的路径                               
                                //const string filePath1 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4795.xml";  

                                //将xml中的值写入字典中
                                nodeidDictionary1 = grpcToolInstance.getNodeIdDictionary(filePath1);

                                logNet.WriteInfo("NodeID Sheet 4795 文件读取成功");
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError("NodeID Sheet 4795 文件读取失败");

                            }

                            //4794
                            try
                            {
                                //EPC中存放的路径
                                const string filePath2 = "/opt/plcnext/apps/GrpcSubscribeNodes_4794.xml";

                                //PC中存放的路径                               
                                //const string filePath2 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4794.xml";  

                                //将xml中的值写入字典中
                                nodeidDictionary2 = grpcToolInstance.getNodeIdDictionary(filePath2);

                                logNet.WriteInfo("NodeID Sheet 4794 文件读取成功");
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError("NodeID Sheet 4794 文件读取失败");

                            }


                            #endregion
                        }
                        stepNumber = 10;

                        break;




                    case 10:
                        {
                            /// <summary>
                            /// 执行初始化
                            /// </summary>

                            logNet.WriteInfo("离心二封设备数采APP已启动");

                            #region 读取Excel （4795 4794 4785对应点表 LXEFData.xlsx； 4752对应点表 LXEFData(4752).xlsx）

                            //string excelFilePath1 = Directory.GetCurrentDirectory() + "\\LXEFData.xlsx";
                            //string excelFilePath2 = Directory.GetCurrentDirectory() + "\\LXEFData(4752).xlsx";     //PC端测试路径
                            
                            string excelFilePath1 = "/opt/plcnext/apps/LXEFData.xlsx";

                                                    
                            XSSFWorkbook excelWorkbook1 = readExcel.connectExcel(excelFilePath1);   // LXEFData(4795 4794 4785)

                            Console.WriteLine("LXEFData read {0}", excelWorkbook1 != null ? "success" : "fail");
                            logNet.WriteInfo("LXEFData 读取 ", excelWorkbook1 != null ? "成功" : "失败");

                          

                            // 给IEC发送 Excel读取成功的信号
                            var tempFlag_finishReadExcelFile = true;

                            listWriteItem.Clear();
                            listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["flag_finishReadExcelFile"], Arp.Type.Grpc.CoreType.CtBoolean, tempFlag_finishReadExcelFile));
                            if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                            {
                                //Console.WriteLine("{0}      flag_finishReadExcelFile写入IEC: success", DateTime.Now);
                                logNet.WriteInfo("[Grpc]", "flag_finishReadExcelFile 写入IEC成功");
                            }
                            else
                            {
                                //Console.WriteLine("{0}      flag_finishReadExcelFile写入IEC: fail", DateTime.Now);
                                logNet.WriteError("[Grpc]", "flag_finishReadExcelFile 写入IEC失败");
                            }


                            #endregion


                            #region 将Excel里的值写入结构体数组中 (4795 4794 4785)

                            // 设备信息（100ms）
                            StationMemory_LXEF1 = readExcel.ReadOneDeviceInfoConSturctInfo_Excel(excelWorkbook1, "设备信息", "工位记忆（BOOL)");

                            // 两个工位（100ms)
                            StationData1_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(1A1B)");
                            StationData2_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(2A2B)");

                            // 非报警信号（1000ms）
                            OEE1_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "OEE(1)", false);
                            OEE2_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "OEE(2)", false);
                            Function_Enable_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "功能开关", false);
                            Production_Data_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "生产统计", false);
                            Life_Management_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "寿命管理", false);

                            // 报警信号（1000ms)
                            Alarm_LXEF1 = readExcel.ReadOneSecAlarm_Excel(excelWorkbook1, "报警信号");

                            #endregion
                       
                         
                            #region 读取并发送两份Excel里的设备总览表

                            deviceInfoStruct1_IEC = readExcel.ReadDeviceInfo_Excel(excelWorkbook1, "离心二封设备总览");   // LXEFData(4795 4794 4785)
                

                            listWriteItem = new List<WriteItem>();

                            //4795
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct1_IEC[0]));
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("ERRO: {0}", e);
                                logNet.WriteError("[Grpc]", "设备编号4795的设备总览信息发送失败，错误原因 : " + e.ToString());
                            }
                            listWriteItem.Clear();


                            //4794
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary2["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct1_IEC[1]));
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("ERRO: {0}", e);
                                logNet.WriteError("[Grpc]", "设备编号4794的设备总览信息发送失败，错误原因 : " + e.ToString());
                            }
                            listWriteItem.Clear();


                          
                            #endregion


                            # region 发送4795的点位名 对应xml 为 nodeidDictionary1

                            keyenceClients.ReadandSendPointName(Production_Data_LXEF1, PointNameStruct_IEC, Production_Data_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1); //生产统计的点位名
                            keyenceClients.ReadandSendPointName(Function_Enable_LXEF1, PointNameStruct_IEC, Function_Enable_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1); //功能开关的点位名
                            keyenceClients.ReadandSendPointName(Life_Management_LXEF1, PointNameStruct_IEC, Life_Management_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1); //寿命管理的点位名
                            keyenceClients.ReadandSendPointName(Alarm_LXEF1, PointNameStruct_IEC, Alarm_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1);                     //报警信号的点位名

                            keyenceClients.ReadandSendPointName(StationData1_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1);        //加工工位一的点位名
                            keyenceClients.ReadandSendPointName(StationData2_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1);        //加工工位二的点位名

                            //将两个OEE的点位名拼成一个 string[]数组后，再发送 对应OEE表格
                            var stringnumber = OEE1_LXEF1.Length + OEE2_LXEF1.Length;
                            var OEEPointName = new string[stringnumber];
                            for (int i = 0; i < OEE1_LXEF1.Length; i++)
                            {
                                OEEPointName[i] = OEE1_LXEF1[i].varAnnotation;
                            }
                            for (int i = 0; i < OEE2_LXEF1.Length; i++)
                            {
                                OEEPointName[i + OEE1_LXEF1.Length] = OEE2_LXEF1[i].varAnnotation;
                            }
                            keyenceClients.ReadandSendPointName(OEEPointName, PointNameStruct_IEC, stringnumber, grpcToolInstance, nodeidDictionary1, grpcDataAccessServiceClient, options1);  //OEE的点位名

                            #endregion

                            #region 发送4794的点位名 对应xml 为 nodeidDictionary2

                            keyenceClients.ReadandSendPointName(Production_Data_LXEF1, PointNameStruct_IEC, Production_Data_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1); //生产统计的点位名
                            keyenceClients.ReadandSendPointName(Function_Enable_LXEF1, PointNameStruct_IEC, Function_Enable_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1); //功能开关的点位名
                            keyenceClients.ReadandSendPointName(Life_Management_LXEF1, PointNameStruct_IEC, Life_Management_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1); //寿命管理的点位名
                            keyenceClients.ReadandSendPointName(Alarm_LXEF1, PointNameStruct_IEC, Alarm_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1);                     //报警信号的点位名

                            keyenceClients.ReadandSendPointName(StationData1_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1);        //加工工位一的点位名
                            keyenceClients.ReadandSendPointName(StationData2_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1);        //加工工位二的点位名

                            keyenceClients.ReadandSendPointName(OEEPointName, PointNameStruct_IEC, stringnumber, grpcToolInstance, nodeidDictionary2, grpcDataAccessServiceClient, options1);  //OEE的点位名

                            #endregion



                            logNet.WriteInfo("点位名发送完毕");


                            stepNumber = 20;
                        }

                        break;

                    case 20:
                        {
                            #region MC连接
                            
                            //_mc[0]:4795 _mc[1]:4794 _mc[2]:4785 _mc[3]:4752

                            for (int i = 0; i < clientNum; i++)
                            {
                                
                                _mc[i] = new KeyenceMcNet(deviceInfoStruct1_IEC[i].strIPAddress, 5000);  //mc协议的端口号5000
                                var retConnect = _mc[i].ConnectServer();
                                //Console.WriteLine("num {0} connect: {1})!", i, retConnect.IsSuccess ? "success" : "fail");
                                logNet.WriteInfo("[MC]","MC[" + i.ToString() + "]连接："+(retConnect.IsSuccess ? "成功" : "失败" ));
                                logNet.WriteInfo("[MC]", "MC[" + i.ToString() + "]连接设备的ip地址为：" + deviceInfoStruct1_IEC[i].strIPAddress);

                            }

                            #endregion
                            stepNumber = 90;
                        }
                        break;


                    case 90:
                        {
                            //线程初始化


                            #region 编号4795

                            // 100ms数据
                            thr[0] = new Thread(() =>
                            {
                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;
                                

                                while (isThreadZeroRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo1(StationMemory_LXEF1, mc, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);                                   
                                    
                                    var ReadObject = "EM5057";   //硬编码
                                    ushort length = 25;

                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF1, ret.Content, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF1, ret.Content, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError("[MC]", ReadObject + "读取失败");
                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (end - start).Duration();
                                                    
                                   
                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("No.4795 Thread 100ms Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);
                                        logNet.WriteInfo("No.4795 Thread 100ms Data Read Time:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            } );

                            // 1000ms数据
                            thr[1] = new Thread(() =>
                            {
                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;

                                while (isThreadOneRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF1, mc, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF1, mc, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //寿命管理
                                     keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF1, mc, ref allDataReadfromMC_4795, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                  
                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    if (OEE_temp1 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, allDataReadfromMC_4795.OEEInfo1Value, 0, OEE_temp1.Length);   //写入缓存区
                                    }

                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);

                                    if (OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp2, 0, allDataReadfromMC_4795.OEEInfo2Value, 0, OEE_temp2.Length);   //写入缓存区

                                    }
                                    
                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];

                                    if (OEE_temp1 != null && OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                        Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

                                    }
                                    

                                    var listWriteItem = new List<WriteItem>();
                                    try
                                    {
                                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["OEE"], Arp.Type.Grpc.CoreType.CtArray, senddata));
                                        var writeItemsArray = listWriteItem.ToArray();
                                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                                    
                                    }
                                    catch (Exception e)
                                    {
                                        // Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary1.GetValueOrDefault("OEE"));
                                        logNet.WriteError("[Grpc]"," OEE数据发送失败：" + e);                                      
                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (end - start).Duration();
                                    
                                   // Console.WriteLine("No.4795 Thread One Second Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }
                                    else
                                    {
                                        logNet.WriteInfo("No.4795 Thread One Second Data Read Time:  " + (dur.TotalMilliseconds).ToString());

                                    }

                                }

                            });

                            #endregion


                            #region 编号4794

                            // 100ms数据
                            thr[2] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;

                                while (isThreadTwoRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo1(StationMemory_LXEF1, mc, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    var ReadObject = "EM5057";
                                    ushort length = 25;
                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF1, ret.Content, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF1, ret.Content, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError("[MC]", ReadObject + "读取失败");
                                        //Console.WriteLine(ReadObject + " Read Failed ");

                                    }


                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (end - start ).Duration();
                                 
                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }
                                    else
                                    {
                                   
                                        logNet.WriteInfo("No.4794 Thread 100ms Data Read Time:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            });

                            // 1000ms数据
                            thr[3] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;

                                while (isThreadThreeRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF1, mc, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF1, mc, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //寿命管理
                                    keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF1, mc, ref allDataReadfromMC_4794, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    if(OEE_temp1 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, allDataReadfromMC_4794.OEEInfo1Value, 0, OEE_temp1.Length);   //写入缓存区
                                    }
                                    

                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);
                                    if(OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp2, 0, allDataReadfromMC_4794.OEEInfo2Value, 0, OEE_temp2.Length);   //写入缓存区
                                    }
                                    

                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];

                                    if(OEE_temp1 != null && OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                        Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

                                    }
                                    
                                    var listWriteItem = new List<WriteItem>();
                                    try
                                    {
                                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["OEE"], Arp.Type.Grpc.CoreType.CtArray, senddata));
                                        var writeItemsArray = listWriteItem.ToArray();
                                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);

                                    }
                                    catch (Exception e)
                                    {                                      
                                        logNet.WriteError("[Grpc]", " OEE数据发送失败：" + e);

                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (end - start).Duration();

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }
                                    else
                                    {
                                        logNet.WriteInfo("No.4794 Thread One Second Data Read Time:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            });

                            #endregion

                    



                            stepNumber = 100;

                        }
                        break;



                    case 100:
                        {
                            #region 开启线程

                            //4795
                            if (thr[0].ThreadState == ThreadState.Unstarted && thr[1].ThreadState == ThreadState.Unstarted 
                                && thr[2].ThreadState == ThreadState.Unstarted && thr[3].ThreadState == ThreadState.Unstarted)
                              
                            {
                                try
                                {
                                    isThreadZeroRunning = true;
                                    thr[0].Start();

                                    isThreadOneRunning = true;
                                    thr[1].Start();


                                    isThreadTwoRunning = true;
                                    thr[2].Start();

                                    isThreadThreeRunning = true;
                                    thr[3].Start();




                                    //APP Status ： running
                                    listWriteItem.Clear();
                                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, 1));
                                    if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                    {
                                        logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                        //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                        logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
                                    }

                                }
                                catch
                                {
                                    Console.WriteLine("Thread quit");

                                    //APP Status ： Error
                                    listWriteItem.Clear();
                                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, -1));
                                    if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                    {
                                        logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                        //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                        logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
                                    }

                                    stepNumber = 1000;
                                    break;

                                }

                            }




                            #endregion

                            #region IEC发送触发信号，重新读取Excel

                            dataAccessServiceReadSingleRequest = new IDataAccessServiceReadSingleRequest();
                            dataAccessServiceReadSingleRequest.PortName = nodeidDictionary1["Switch_ReadExcelFile"];
                            if (grpcToolInstance.ReadSingleDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceReadSingleRequest, new IDataAccessServiceReadSingleResponse(), options1).BoolValue)
                            {
                                //复位信号点:Switch_WriteExcelFile                               
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["Switch_ReadExcelFile"], Arp.Type.Grpc.CoreType.CtBoolean, false)); //Write Data to DataAccessService                                 
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //Console.WriteLine("{0}      Switch_ReadExcelFile写入IEC: success", DateTime.Now);
                                    logNet.WriteInfo("[Grpc]", "Switch_ReadExcelFile 写入IEC成功");
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      Switch_ReadExcelFile写入IEC: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "Switch_ReadExcelFile 写入IEC失败");
                                }


                                //停止线程
                                isThreadZeroRunning = false;
                                isThreadOneRunning = false;
                                isThreadTwoRunning = false;
                                isThreadThreeRunning = false;


                                for (int i = 0; i < clientNum; i++)
                                {
                                    _mc[i].ConnectClose();
                                    //Console.WriteLine(" MC {0} Connect closed", i);
                                    logNet.WriteInfo("[MC]", "MC连接断开" + i.ToString());
                                }

                                Thread.Sleep(1000);//等待线程退出

                                stepNumber = 6;
                            }

                            #endregion


                            #region 检测PLCnext和Keyence PLC之间的连接

                            for (int i=0; i <clientNum;i ++)
                            {
                                IPStatus iPStatus;
                                iPStatus = _mc[i].IpAddressPing();  //判断与PLC的物理连接状态

                                string[] plcErrors = {
                                                        "Ping Keyence PLC 4795 failed",
                                                        "Ping Keyence PLC 4794 failed"                                                      
                                                      };

                                if (iPStatus != 0)
                                {                                 
                                    logNet.WriteError("[MC]", plcErrors[i]);

                                }
                              
                            }

                            #endregion


                            #region IEC发送触发信号,将采集值写入Excel

                            dataAccessServiceReadSingleRequest = new IDataAccessServiceReadSingleRequest();
                            dataAccessServiceReadSingleRequest.PortName = nodeidDictionary1["Switch_WriteExcelFile"];
                            if (grpcToolInstance.ReadSingleDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceReadSingleRequest, new IDataAccessServiceReadSingleResponse(), options1).BoolValue)
                            {
                                //复位信号点: Switch_WriteExcelFile
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["Switch_WriteExcelFile"], Arp.Type.Grpc.CoreType.CtBoolean, false)); //Write Data to DataAccessService                                 
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //Console.WriteLine("{0}      Switch_WriteExcelFile: success", DateTime.Now);
                                    logNet.WriteInfo("[Grpc]", "Switch_WriteExcelFile 写入IEC成功");
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      Switch_WriteExcelFile: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "Switch_WriteExcelFile 写入IEC失败");
                                }

                                //将读取的值写入Excel 
                                thr[4] = new Thread(() =>
                                {

                                    var ExcelPath1 = "/opt/plcnext/apps/LXEFData.xlsx";
                                    var ExcelPath2 = "/opt/plcnext/apps/LXEFData(4752).xlsx";

                                    //var ExcelPath1 = Directory.GetCurrentDirectory() + "\\LXEFData.xlsx";
                                    //var ExcelPath2 = Directory.GetCurrentDirectory() + "\\LXEFData(4752).xlsx";     //PC端测试路径

                                    //将数据缓存区的值赋给临时变量
                                    var allDataReadfromMC_temp_4795 = allDataReadfromMC_4795;
                                    var allDataReadfromMC_temp_4794 = allDataReadfromMC_4794;




                                    #region 将数据缓存区的值写入Excel(4795)

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "设备信息", "采集值（4795）", allDataReadfromMC_temp_4795.DeviceInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 工位记忆采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 工位记忆采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(1A1B)", "采集值（4795）", allDataReadfromMC_temp_4795.Station1A1BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(1A1B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(1A1B)采集值写入Excel失败原因: " + e);

                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(2A2B)", "采集值（4795）", allDataReadfromMC_temp_4795.Station2A2BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(2A2B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(2A2B)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(1)", "采集值（4795）", allDataReadfromMC_temp_4795.OEEInfo1Value);
                                        logNet.WriteInfo("WriteData", "编号4795 OEE(1)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 OEE(1)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(2)", "采集值（4795）", allDataReadfromMC_temp_4795.OEEInfo2Value);
                                        logNet.WriteInfo("WriteData", "编号4795 OEE(2)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        //Console.WriteLine("加工工位（顶封）采集值写入Excel失败原因: {0} ", e);
                                        logNet.WriteError("WriteData", "编号4795 OEE(2)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "功能开关", "采集值（4795）", allDataReadfromMC_temp_4795.FunctionEnableValue);
                                        logNet.WriteInfo("WriteData", "编号4795 功能开关采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 功能开关采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "生产统计", "采集值（4795）", allDataReadfromMC_temp_4795.ProductionDataValue);
                                        logNet.WriteInfo("WriteData", "编号4795 生产统计采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 生产统计采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "寿命管理", "采集值（4795）", allDataReadfromMC_temp_4795.LifeManagementValue);
                                        logNet.WriteInfo("WriteData", "编号4795 寿命管理采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 寿命管理采集值写入Excel失败原因: " + e);
                                    }

                                    #endregion


                                    #region 将数据缓存区的值写入Excel(4794)

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "设备信息", "采集值（4794）", allDataReadfromMC_temp_4794.DeviceInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4794 工位记忆采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 工位记忆采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(1A1B)", "采集值（4794）", allDataReadfromMC_temp_4794.Station1A1BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4794 加工工位(1A1B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 加工工位(1A1B)采集值写入Excel失败原因: " + e);

                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(2A2B)", "采集值（4794）", allDataReadfromMC_temp_4794.Station2A2BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4794 加工工位(2A2B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 加工工位(2A2B)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(1)", "采集值（4794）", allDataReadfromMC_temp_4794.OEEInfo1Value);
                                        logNet.WriteInfo("WriteData", "编号4794 OEE(1)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 OEE(1)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(2)", "采集值（4794）", allDataReadfromMC_temp_4794.OEEInfo2Value);
                                        logNet.WriteInfo("WriteData", "编号4794 OEE(2)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        //Console.WriteLine("加工工位（顶封）采集值写入Excel失败原因: {0} ", e);
                                        logNet.WriteError("WriteData", "编号4794 OEE(2)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "功能开关", "采集值（4794）", allDataReadfromMC_temp_4794.FunctionEnableValue);
                                        logNet.WriteInfo("WriteData", "编号4794 功能开关采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 功能开关采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "生产统计", "采集值（4794）", allDataReadfromMC_temp_4794.ProductionDataValue);
                                        logNet.WriteInfo("WriteData", "编号4794 生产统计采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 生产统计采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "寿命管理", "采集值（4794）", allDataReadfromMC_temp_4794.LifeManagementValue);
                                        logNet.WriteInfo("WriteData", "编号4794 寿命管理采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4794 寿命管理采集值写入Excel失败原因: " + e);
                                    }

                                    #endregion

                              

                                    //给IEC写入 采集值写入成功的信号
                                    var tempFlag_finishWriteExcelFile = true;

                                    listWriteItem.Clear();
                                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["flag_finishWriteExcelFile"], Arp.Type.Grpc.CoreType.CtBoolean, tempFlag_finishWriteExcelFile));
                                    if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                    {
                                        //Console.WriteLine("{0}      flag_finishWriteExcelFile写入IEC: success", DateTime.Now);
                                        logNet.WriteInfo("[Grpc]", "flag_finishWriteExcelFile 写入IEC成功");
                                    }
                                    else
                                    {
                                        //Console.WriteLine("{0}      flag_finishWriteExcelFile写入IEC: fail", DateTime.Now);
                                        logNet.WriteError("[Grpc]", "flag_finishWriteExcelFile 写入IEC失败");
                                    }

                                    IecTriggersNumber = 0;  //为了防止IEC连续两次赋值true

                                });

                                IecTriggersNumber++;

                                if (IecTriggersNumber == 1)
                                {
                                    thr[4].Start();
                                }

                            }

                            #endregion


                            Thread.Sleep(1000);

                            break;
                        }


                    case 1000:      //异常处理
                                    //信号复位
                                    //CIP连接断了


                        break;

                    case 10000:      //复位处理

                        break;


                }


            }

        }






    }
}