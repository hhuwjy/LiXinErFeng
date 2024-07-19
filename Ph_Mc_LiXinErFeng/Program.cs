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
using NPOI.OpenXmlFormats.Wordprocessing;

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
        static int thrNum = 7;  //开启三个线程
        static Thread[] thr = new Thread[thrNum];

        //创建nodeID字典 (读取XML用）
        public static Dictionary<string, string> nodeidDictionary1;
        public static Dictionary<string, string> nodeidDictionary2;


        //读取Excel用
        static ReadExcel readExcel = new ReadExcel();

        #region 从Excel解析来的数据实例化 (4795 4785 )

        //设备信息数据
        static DeviceInfoConSturct_MC[] StationMemory_LXEF1;

        //四个工位数据
        static StationInfoStruct_MC[] StationData1A_LXEF1;
        static StationInfoStruct_MC[] StationData1B_LXEF1;
        static StationInfoStruct_MC[] StationData2A_LXEF1;
        static StationInfoStruct_MC[] StationData2B_LXEF1;

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


        // 设备总览
        static DeviceInfoStruct_IEC[] deviceInfoStruct1_IEC;    //LXEFData(4795)
        static DeviceInfoStruct_IEC[] deviceInfoStruct2_IEC;    //LXEFData(4795)
        #endregion


        static void Main(string[] args)
        {
            if (!HslCommunication.Authorization.SetAuthorizationCode("b7fb5c78-1c92-41f5-9fd3-de73313ba9c2"))
            {
                Console.WriteLine("授权失败!当前程序只能使用24小时!");
            }
          

            int stepNumber = 5;


            List<WriteItem> listWriteItem = new List<WriteItem>();
            IDataAccessServiceReadSingleRequest dataAccessServiceReadSingleRequest = new IDataAccessServiceReadSingleRequest();

            bool isThreadZeroRunning = false;
            bool isThreadOneRunning = false;
            bool isThreadTwoRunning = false;
            bool isThreadThreeRunning = false;
            bool isThreadFourRunning = false;
            bool isThreadFiveRunning = false;
            int IecTriggersNumber = 0;

            //采集值缓存区，需要写入Excel
            AllDataReadfromMC allDataReadfromMC_4795 = new AllDataReadfromMC();
            AllDataReadfromMC allDataReadfromMC_4785 = new AllDataReadfromMC();


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

                            //4785
                            try
                            {
                                //EPC中存放的路径
                                const string filePath2 = "/opt/plcnext/apps/GrpcSubscribeNodes_4785.xml";

                                //PC中存放的路径                               
                                //const string filePath2 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4785.xml";  

                                //将xml中的值写入字典中
                                nodeidDictionary2 = grpcToolInstance.getNodeIdDictionary(filePath2);

                                logNet.WriteInfo("NodeID Sheet 4785 文件读取成功");
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError("NodeID Sheet 4785 文件读取失败");

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

                            #region 读取Excel （4795 4785 4785对应点表 LXEFData.xlsx； 4752对应点表 LXEFData(4752).xlsx）

                            //string excelFilePath1 = Directory.GetCurrentDirectory() + "\\LXEFData(4795).xlsx";
                            //string excelFilePath2 = Directory.GetCurrentDirectory() + "\\LXEFData(4785).xlsx";


                            string excelFilePath1 = "/opt/plcnext/apps/LXEFData(4795).xlsx";
                            string excelFilePath2 = "/opt/plcnext/apps/LXEFData(4785).xlsx";


                            XSSFWorkbook excelWorkbook1 = readExcel.connectExcel(excelFilePath1);   // LXEFData(4795 4785 4785)

                            Console.WriteLine("LXEFData(4795) read {0}", excelWorkbook1 != null ? "success" : "fail");
                            logNet.WriteInfo("LXEFData(4795) 读取 ", excelWorkbook1 != null ? "成功" : "失败");

                            XSSFWorkbook excelWorkbook2 = readExcel.connectExcel(excelFilePath2);   // LXEFData(4795 4785 4785)

                            Console.WriteLine("LXEFData(4785) read {0}", excelWorkbook2 != null ? "success" : "fail");
                            logNet.WriteInfo("LXEFData(4785) 读取 ", excelWorkbook2 != null ? "成功" : "失败");



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


                            #region 将Excel里的值写入结构体数组中 (4795 4785 )

                            // 设备信息（100ms）
                            StationMemory_LXEF1 = readExcel.ReadOneDeviceInfoConSturctInfo_Excel(excelWorkbook1, "设备信息", "工位记忆（BOOL)");

                            //四个工位（100ms)
                            StationData1A_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(1A)");
                            StationData1B_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(1B)");
                            StationData2A_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(2A)");
                            StationData2B_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(2B)");


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

                            var deviceInfoStructList_IEC = new DeviceInfoStructList_IEC();
                            listWriteItem = new List<WriteItem>();

                            //4795 的设备信息表
                            deviceInfoStruct1_IEC = readExcel.ReadDeviceInfo_Excel(excelWorkbook1, "离心二封设备总览");   // LXEFData(4795)

                            deviceInfoStructList_IEC.iCount = (short)deviceInfoStruct1_IEC.Length;
                            for (int i = 0; i < deviceInfoStruct1_IEC.Length; i++)
                            {
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strDeviceName = deviceInfoStruct1_IEC[i].strDeviceName;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strDeviceCode = deviceInfoStruct1_IEC[i].strDeviceCode;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].iStationCount = deviceInfoStruct1_IEC[i].iStationCount;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strPLCType = deviceInfoStruct1_IEC[i].strPLCType;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strProtocol = deviceInfoStruct1_IEC[i].strProtocol;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strIPAddress = deviceInfoStruct1_IEC[i].strIPAddress;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].iPort = deviceInfoStruct1_IEC[i].iPort;
                            }
                           
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStructList_IEC));
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


                            //4785 的设备信息表

                            deviceInfoStructList_IEC = new DeviceInfoStructList_IEC();
                            deviceInfoStruct2_IEC = readExcel.ReadDeviceInfo_Excel(excelWorkbook2, "离心二封设备总览");   // LXEFData(4785)

                            deviceInfoStructList_IEC.iCount = (short)deviceInfoStruct2_IEC.Length;
                            for (int i = 0; i < deviceInfoStruct2_IEC.Length; i++)
                            {
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strDeviceName = deviceInfoStruct2_IEC[i].strDeviceName;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strDeviceCode = deviceInfoStruct2_IEC[i].strDeviceCode;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].iStationCount = deviceInfoStruct2_IEC[i].iStationCount;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strPLCType = deviceInfoStruct2_IEC[i].strPLCType;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strProtocol = deviceInfoStruct2_IEC[i].strProtocol;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].strIPAddress = deviceInfoStruct2_IEC[i].strIPAddress;
                                deviceInfoStructList_IEC.arrDeviceInfo[i].iPort = deviceInfoStruct2_IEC[i].iPort;
                            }

                           
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary2["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStructList_IEC));
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("ERRO: {0}", e);
                                logNet.WriteError("[Grpc]", "设备编号4785的设备总览信息发送失败，错误原因 : " + e.ToString());
                            }
                            listWriteItem.Clear();


                            #endregion

                            stepNumber = 15;
                        }

                        break;

                    case 15:
                        {
                            //发送4795 和 4785 的点位名 对应xml 为 nodeidDictionary1

                            #region 读取并发送1000ms数据的点位名

                            //实例化发给IEC的 1000ms数据的点位名 结构体
                            var OneSecNameStruct = new OneSecPointNameStruct_IEC();

                            // 功能安全、生产统计 、寿命管理 和报警信息 的点位名
                            keyenceClients.ReadPointName(Function_Enable_LXEF1, ref OneSecNameStruct);
                            keyenceClients.ReadPointName(Production_Data_LXEF1, ref OneSecNameStruct);
                            keyenceClients.ReadPointName(Life_Management_LXEF1, ref OneSecNameStruct);
                            keyenceClients.ReadPointName(Alarm_LXEF1, ref OneSecNameStruct);

                            //OEE的点位名
                            var stringnumber = OEE1_LXEF1.Length + OEE2_LXEF1.Length;
                            List<OneSecInfoStruct_MC[]> OEEGroups = new List<OneSecInfoStruct_MC[]> { OEE1_LXEF1, OEE2_LXEF1 };

                            keyenceClients.ReadPointName(OEEGroups, stringnumber, ref OneSecNameStruct);

                            //Grpc发送1000ms数据点位名结构体
                            listWriteItem.Clear();
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1.GetValueOrDefault("OneSecNameStruct"), Arp.Type.Grpc.CoreType.CtStruct, OneSecNameStruct));
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("[Grpc]", " 1000ms数据的点位名发送失败：" + e);
                                //Console.WriteLine("ERRO: {0}", e);
                            }

                            #endregion

                            #region 读取并发送四个加工工位的点位名

                            var ProcessStationNameStruct = new ProcessStationNameStruct_IEC();

                            List<StationInfoStruct_MC[]> StationDataStruct = new List<StationInfoStruct_MC[]>
                            {  StationData1A_LXEF1, StationData1B_LXEF1, StationData2A_LXEF1,StationData2B_LXEF1 };

                            keyenceClients.ReadPointName(StationDataStruct, ref ProcessStationNameStruct);

                            //Grpc发送1000ms数据点位名结构体
                            listWriteItem.Clear();
                            try
                            {
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1.GetValueOrDefault("ProcessStationNameStruct"), Arp.Type.Grpc.CoreType.CtStruct, ProcessStationNameStruct));
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("[Grpc]", " 加工工位的点位名发送失败：" + e);
                                //Console.WriteLine("ERRO: {0}", e);
                            }

                            #endregion

                            logNet.WriteInfo("点位名发送完毕");

                            stepNumber = 20;
                        }

                        break;

                    case 20:
                        {
                            #region MC连接

                            //_mc[0]:4795 _mc[1]:4785 _mc[2]:4785 _mc[3]:4752


                            var i = 0;     //_mc[0]:4795
                            _mc[i] = new KeyenceMcNet(deviceInfoStruct1_IEC[0].strIPAddress, 5000);  //mc协议的端口号5000
                            var retConnect = _mc[i].ConnectServer();
                            //Console.WriteLine("num {0} connect: {1})!", i, retConnect.IsSuccess ? "success" : "fail");
                            logNet.WriteInfo("[MC]", "MC[" + i.ToString() + "]连接：" + (retConnect.IsSuccess ? "成功" : "失败"));
                            logNet.WriteInfo("[MC]", "MC[" + i.ToString() + "]连接设备的ip地址为：" + deviceInfoStruct1_IEC[0].strIPAddress);

                            i = 1;     //_mc[0]:4785
                            _mc[i] = new KeyenceMcNet(deviceInfoStruct2_IEC[0].strIPAddress, 5000);  //mc协议的端口号5000
                            retConnect = _mc[i].ConnectServer();
                            //Console.WriteLine("num {0} connect: {1})!", i, retConnect.IsSuccess ? "success" : "fail");
                            logNet.WriteInfo("[MC]", "MC[" + i.ToString() + "]连接：" + (retConnect.IsSuccess ? "成功" : "失败"));
                            logNet.WriteInfo("[MC]", "MC[" + i.ToString() + "]连接设备的ip地址为：" + deviceInfoStruct2_IEC[0].strIPAddress);





                            #endregion
                            stepNumber = 90;
                        }
                        break;


                    case 90:
                        {
                            //线程初始化


                            #region 编号4795

                            // 100ms设备数据
                            thr[0] = new Thread(() =>
                            {
                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;

                                var StationListlnfo = new UDT_StationListlnfo(); // 实例化 设备信息采集值结构体
                                //var ProcessStationDataValue = new UDT_ProcessStationDataValue();   //实例化 加工工位采集值结构体

                                while (isThreadZeroRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    // 读取并发送设备信息表里的采集值 （只有工位记忆）
                                    keyenceClients.ReadandSendDeviceInfo1(StationMemory_LXEF1, mc, ref allDataReadfromMC_4795, ref StationListlnfo, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

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
                                        logNet.WriteInfo("No.4795 Thread 100ms 设备信息读取时间:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            } );



                            // 1000ms数据
                            thr[1] = new Thread(() =>
                            {
                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;
                                var DeviceDataStruct = new DeviceDataStruct_IEC();


                                while (isThreadOneRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadConOneSecData(Function_Enable_LXEF1,  mc, ref allDataReadfromMC_4795, ref  DeviceDataStruct);
                                   
                                    //生产统计
                                    keyenceClients.ReadConOneSecData(Production_Data_LXEF1,  mc, ref  allDataReadfromMC_4795, ref  DeviceDataStruct);
                                   
                                    //寿命管理
                                     keyenceClients.ReadDisOneSecData(Life_Management_LXEF1, mc, ref allDataReadfromMC_4795, ref  DeviceDataStruct);
                                   
                                    //报警信号
                                    keyenceClients.ReadAlarmData(Alarm_LXEF1, mc, ref DeviceDataStruct);
                                  
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
                                   

                                    if (OEE_temp1 != null && OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, DeviceDataStruct.Value_OEE, 0, OEE_temp1.Length);

                                        Array.Copy(OEE_temp2, 0, DeviceDataStruct.Value_OEE, OEE_temp1.Length, OEE_temp2.Length);

                                    }


                                    // Grpc 发送1000ms数据采集值

                                    listWriteItem.Clear();

                                    try
                                    {
                                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["OneSecDataValue"], Arp.Type.Grpc.CoreType.CtStruct, DeviceDataStruct));
                                        var writeItemsArray = listWriteItem.ToArray();
                                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                                    }
                                    catch (Exception e)
                                    {
                                        //logNet.WriteError("[Grpc]", "OEE数据发送失败：" + e);
                                        Console.WriteLine("ERRO: {0}", e, nodeidDictionary.GetValueOrDefault("OneSecDataValue"));
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
                                        logNet.WriteInfo("No.4795 Thread 1000ms 数据读取时间:  " + (dur.TotalMilliseconds).ToString());

                                    }

                                }

                            });

                            // 100ms 加工工位数据

                            thr[2] = new Thread(() =>
                            {

                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;

                               // var StationListlnfo = new UDT_StationListlnfo(); // 实例化 设备信息采集值结构体
                                var ProcessStationDataValue = new UDT_ProcessStationDataValue();   //实例化 加工工位采集值结构体

                                while (isThreadTwoRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    #region 读取并发送四个加工工位的信息

                                    var ReadObject = "EM5057";   //硬编码
                                    ushort length = 25;
                                    OperateResult<short[]> ret_EM = mc.ReadInt16(ReadObject, length);

                                    ReadObject = "DM9500";   //硬编码
                                    length = 308;
                                    OperateResult<short[]> ret_DM = mc.ReadInt16(ReadObject, length);

                                    ReadObject = "MR6008";   //硬编码
                                    length = (ushort)(keyenceClients.CalculateIndex_H(6008, 9015) + 1);
                                    OperateResult<bool[]> ret_MR = mc.ReadBool(ReadObject, length);

                                    ProcessStationDataValue.iDataCount = 4;    // 一共四个加工工位

                                    if (ret_EM.IsSuccess && ret_DM.IsSuccess && ret_MR.IsSuccess)
                                    {

                                        keyenceClients.WriteStationData(StationData1A_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4795, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData1B_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4795, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData2A_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4795, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData2B_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4795, ref ProcessStationDataValue);

                                        //Grpc 发送加工工位数据采集值

                                        listWriteItem.Clear();
                                        try
                                        {
                                            listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["ProcessStationData"], Arp.Type.Grpc.CoreType.CtStruct, ProcessStationDataValue));
                                            var writeItemsArray = listWriteItem.ToArray();
                                            var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                            bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                                        }
                                        catch (Exception e)
                                        {
                                            logNet.WriteError("[Grpc]", "加工工位数据发送失败：" + e);

                                        }

                                    }
                                    else
                                    {
                                        logNet.WriteError("[MC]", "加工工位数据读取失败");

                                    }
                                    #endregion


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
                                        logNet.WriteInfo("No.4795 Thread 100ms 加工工位数据读取时间:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            });

                          


                            #endregion


                            #region 编号4785

                            // 100ms数据
                            thr[3] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;
                                var StationListlnfo = new UDT_StationListlnfo(); // 实例化 设备信息采集值结构体
                                //var ProcessStationDataValue = new UDT_ProcessStationDataValue();   //实例化 加工工位采集值结构体

                                while (isThreadThreeRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    // 读取并发送设备信息表里的采集值 （只有工位记忆）
                                    keyenceClients.ReadandSendDeviceInfo1(StationMemory_LXEF1, mc, ref allDataReadfromMC_4785, ref StationListlnfo, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

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
                                   
                                        logNet.WriteInfo("No.4785 Thread 100ms 设备信息读取时间:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            });

                            // 1000ms数据
                            thr[4] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;
                                var DeviceDataStruct = new DeviceDataStruct_IEC();

                                while (isThreadFourRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadConOneSecData(Function_Enable_LXEF1, mc, ref allDataReadfromMC_4785, ref DeviceDataStruct);

                                    //生产统计
                                    keyenceClients.ReadConOneSecData(Production_Data_LXEF1, mc, ref allDataReadfromMC_4785, ref DeviceDataStruct);

                                    //寿命管理
                                    keyenceClients.ReadDisOneSecData(Life_Management_LXEF1, mc, ref allDataReadfromMC_4785, ref DeviceDataStruct);

                                    //报警信号
                                    keyenceClients.ReadAlarmData(Alarm_LXEF1, mc, ref DeviceDataStruct);

                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    if (OEE_temp1 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, allDataReadfromMC_4785.OEEInfo1Value, 0, OEE_temp1.Length);   //写入缓存区
                                    }

                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);

                                    if (OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp2, 0, allDataReadfromMC_4785.OEEInfo2Value, 0, OEE_temp2.Length);   //写入缓存区

                                    }


                                    if (OEE_temp1 != null && OEE_temp2 != null)
                                    {
                                        Array.Copy(OEE_temp1, 0, DeviceDataStruct.Value_OEE, 0, OEE_temp1.Length);

                                        Array.Copy(OEE_temp2, 0, DeviceDataStruct.Value_OEE, OEE_temp1.Length, OEE_temp2.Length);

                                    }


                                    // Grpc 发送1000ms数据采集值

                                    listWriteItem.Clear();

                                    try
                                    {
                                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["OneSecDataValue"], Arp.Type.Grpc.CoreType.CtStruct, DeviceDataStruct));
                                        var writeItemsArray = listWriteItem.ToArray();
                                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                                    }
                                    catch (Exception e)
                                    {
                                        //logNet.WriteError("[Grpc]", "OEE数据发送失败：" + e);
                                        Console.WriteLine("ERRO: {0}", e, nodeidDictionary.GetValueOrDefault("OneSecDataValue"));
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
                                        logNet.WriteInfo("No.4785 Thread One Second Data Read Time:  " + (dur.TotalMilliseconds).ToString());
                                    }

                                }

                            });

                            thr[5] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;
                                var StationListlnfo = new UDT_StationListlnfo(); // 实例化 设备信息采集值结构体
                                var ProcessStationDataValue = new UDT_ProcessStationDataValue();   //实例化 加工工位采集值结构体

                                while (isThreadFiveRunning)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    #region 读取并发送四个加工工位的信息

                                    var ReadObject = "EM5057";   //硬编码
                                    ushort length = 25;
                                    OperateResult<short[]> ret_EM = mc.ReadInt16(ReadObject, length);

                                    ReadObject = "DM9500";   //硬编码
                                    length = 308;
                                    OperateResult<short[]> ret_DM = mc.ReadInt16(ReadObject, length);

                                    ReadObject = "MR6008";   //硬编码
                                    length = (ushort)(keyenceClients.CalculateIndex_H(6008, 9015) + 1);
                                    OperateResult<bool[]> ret_MR = mc.ReadBool(ReadObject, length);

                                    ProcessStationDataValue.iDataCount = 4;    // 一共四个加工工位

                                    if (ret_EM.IsSuccess && ret_DM.IsSuccess && ret_MR.IsSuccess)
                                    {

                                        keyenceClients.WriteStationData(StationData1A_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4785, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData1B_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4785, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData2A_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4785, ref ProcessStationDataValue);
                                        keyenceClients.WriteStationData(StationData2B_LXEF1, ret_EM.Content, ret_DM.Content, ret_MR.Content, ref allDataReadfromMC_4785, ref ProcessStationDataValue);

                                        //Grpc 发送加工工位数据采集值

                                        listWriteItem.Clear();
                                        try
                                        {
                                            listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["ProcessStationData"], Arp.Type.Grpc.CoreType.CtStruct, ProcessStationDataValue));
                                            var writeItemsArray = listWriteItem.ToArray();
                                            var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                            bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                                        }
                                        catch (Exception e)
                                        {
                                            logNet.WriteError("[Grpc]", "加工工位数据发送失败：" + e);

                                        }

                                    }
                                    else
                                    {
                                        logNet.WriteError("[MC]", "加工工位数据读取失败");

                                    }
                                    #endregion


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

                                        logNet.WriteInfo("No.4785 Thread 100ms 加工工位数据读取时间:  " + (dur.TotalMilliseconds).ToString());
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
                                && thr[2].ThreadState == ThreadState.Unstarted && thr[3].ThreadState == ThreadState.Unstarted
                                && thr[4].ThreadState == ThreadState.Unstarted && thr[5].ThreadState == ThreadState.Unstarted)
                              
                            {
                                try
                                {
                                    isThreadZeroRunning = true;  //4795的设备信息
                                    thr[0].Start();

                                    isThreadOneRunning = true; //4795的1000ms数据
                                    thr[1].Start();

                                    isThreadTwoRunning = true;  //4795的加工工位数据
                                    thr[2].Start();



                                    isThreadThreeRunning = true; //4785的设备信息
                                    thr[3].Start();

                                    isThreadFourRunning = true; //4785的1000ms数据
                                    thr[4].Start();

                                    isThreadFiveRunning = true; //4785的加工工位数据 
                                    thr[5].Start();






                                    //APP Status ： running
                                    listWriteItem.Clear();
                                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, 1));
                                    if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                    {
                                        //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                        //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                        continue;
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
                                        //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                        //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                        continue;
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
                                    continue;
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
                                isThreadFourRunning = false;
                                isThreadFiveRunning = false;

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

                            
                            IPStatus iPStatus_4795;
                            IPStatus iPStatus_4785;
                            iPStatus_4795 = _mc[0].IpAddressPing();  //判断与PLC的物理连接状态
                            iPStatus_4785 = _mc[1].IpAddressPing();  //判断与PLC的物理连接状态

                            if (iPStatus_4795 != 0 && iPStatus_4785 == 0)
                            {                                 
                                logNet.WriteError("[MC]", "Ping Keyence PLC 4795 failed");

                                //APP Status ： Error
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, -2));
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                    //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    continue;
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
                                }

                            }

                            if (iPStatus_4785 != 0 && iPStatus_4795 == 0)
                            {
                                logNet.WriteError("[MC]", "Ping Keyence PLC 4785 failed");

                                //APP Status ： Error
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, -3));
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                    //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    continue;
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
                                }

                            }
                            if (iPStatus_4785 != 0 && iPStatus_4795 != 0)
                            {
                                logNet.WriteError("[MC]", "Ping Keyence PLC 4785 failed");

                                //APP Status ： Error
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, -4));
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                    //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    continue;
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
                                }

                            }
                             if(iPStatus_4785 == 0 && iPStatus_4795 == 0)
                            {
                                //APP Status ： running
                                listWriteItem.Clear();
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["AppStatus"], Arp.Type.Grpc.CoreType.CtInt32, 1));
                                if (grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, grpcToolInstance.ServiceWriteRequestAddDatas(listWriteItem.ToArray()), new IDataAccessServiceWriteResponse(), options1))
                                {
                                    //logNet.WriteInfo("[Grpc]", "AppStatus 写入IEC成功");
                                    //Console.WriteLine("{0}      AppStatus写入IEC: success", DateTime.Now);
                                    continue;
                                }
                                else
                                {
                                    //Console.WriteLine("{0}      AppStatus写入IEC: fail", DateTime.Now);
                                    logNet.WriteError("[Grpc]", "AppStatus 写入IEC失败");
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
                                thr[6] = new Thread(() =>
                                {

                                    var ExcelPath1 = "/opt/plcnext/apps/LXEFData(4795).xlsx";
                                    var ExcelPath2 = "/opt/plcnext/apps/LXEFData(4785).xlsx";


                                    //var ExcelPath1 = Directory.GetCurrentDirectory() + "\\LXEFData.xlsx";
                                    //var ExcelPath2 = Directory.GetCurrentDirectory() + "\\LXEFData(4752).xlsx";     //PC端测试路径

                                    //将数据缓存区的值赋给临时变量
                                    var allDataReadfromMC_temp_4795 = allDataReadfromMC_4795;
                                    var allDataReadfromMC_temp_4785 = allDataReadfromMC_4785;




                                    #region 将数据缓存区的值写入Excel(4795)

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "设备信息", "采集值", allDataReadfromMC_temp_4795.DeviceInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 工位记忆采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 工位记忆采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(1A)", "采集值", allDataReadfromMC_temp_4795.Station1AInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(1A)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(1A)采集值写入Excel失败原因: " + e);

                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(1B)", "采集值", allDataReadfromMC_temp_4795.Station1BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(1B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(1B)采集值写入Excel失败原因: " + e);

                                    }
                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(2A)", "采集值", allDataReadfromMC_temp_4795.Station2AInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(2A)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(2A)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "加工工位(2B)", "采集值", allDataReadfromMC_temp_4795.Station2BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4795 加工工位(2B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 加工工位(2B)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(1)", "采集值", allDataReadfromMC_temp_4795.OEEInfo1Value);
                                        logNet.WriteInfo("WriteData", "编号4795 OEE(1)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 OEE(1)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "OEE(2)", "采集值", allDataReadfromMC_temp_4795.OEEInfo2Value);
                                        logNet.WriteInfo("WriteData", "编号4795 OEE(2)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        //Console.WriteLine("加工工位（顶封）采集值写入Excel失败原因: {0} ", e);
                                        logNet.WriteError("WriteData", "编号4795 OEE(2)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "功能开关", "采集值", allDataReadfromMC_temp_4795.FunctionEnableValue);
                                        logNet.WriteInfo("WriteData", "编号4795 功能开关采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 功能开关采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "生产统计", "采集值", allDataReadfromMC_temp_4795.ProductionDataValue);
                                        logNet.WriteInfo("WriteData", "编号4795 生产统计采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 生产统计采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath1, "寿命管理", "采集值", allDataReadfromMC_temp_4795.LifeManagementValue);
                                        logNet.WriteInfo("WriteData", "编号4795 寿命管理采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4795 寿命管理采集值写入Excel失败原因: " + e);
                                    }

                                    #endregion


                                    #region 将数据缓存区的值写入Excel(4785)

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "设备信息", "采集值", allDataReadfromMC_temp_4785.DeviceInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4785 工位记忆采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 工位记忆采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "加工工位(1A)", "采集值", allDataReadfromMC_temp_4785.Station1AInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4785 加工工位(1A)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 加工工位(1A)采集值写入Excel失败原因: " + e);

                                    }
                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "加工工位(1B)", "采集值", allDataReadfromMC_temp_4785.Station1BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4785 加工工位(1B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 加工工位(1B)采集值写入Excel失败原因: " + e);

                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "加工工位(2A)", "采集值", allDataReadfromMC_temp_4785.Station2AInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4785 加工工位(2A)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 加工工位(2A)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "加工工位(2B)", "采集值", allDataReadfromMC_temp_4785.Station2BInfoValue);
                                        logNet.WriteInfo("WriteData", "编号4785 加工工位(2B)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 加工工位(2B)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "OEE(1)", "采集值", allDataReadfromMC_temp_4785.OEEInfo1Value);
                                        logNet.WriteInfo("WriteData", "编号4785 OEE(1)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 OEE(1)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "OEE(2)", "采集值", allDataReadfromMC_temp_4785.OEEInfo2Value);
                                        logNet.WriteInfo("WriteData", "编号4785 OEE(2)采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        //Console.WriteLine("加工工位（顶封）采集值写入Excel失败原因: {0} ", e);
                                        logNet.WriteError("WriteData", "编号4785 OEE(2)采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "功能开关", "采集值", allDataReadfromMC_temp_4785.FunctionEnableValue);
                                        logNet.WriteInfo("WriteData", "编号4785 功能开关采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 功能开关采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "生产统计", "采集值", allDataReadfromMC_temp_4785.ProductionDataValue);
                                        logNet.WriteInfo("WriteData", "编号4785 生产统计采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 生产统计采集值写入Excel失败原因: " + e);
                                    }

                                    try
                                    {
                                        var result = readExcel.setExcelCellValue(ExcelPath2, "寿命管理", "采集值", allDataReadfromMC_temp_4785.LifeManagementValue);
                                        logNet.WriteInfo("WriteData", "编号4785 寿命管理采集值写入Excel: " + (result ? "成功" : "失败"));
                                    }
                                    catch (Exception e)
                                    {
                                        logNet.WriteError("WriteData", "编号4785 寿命管理采集值写入Excel失败原因: " + e);
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
                                    thr[6].Start();
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