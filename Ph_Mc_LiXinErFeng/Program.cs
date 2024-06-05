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

namespace Ph_Mc_LiXinErFeng
{
    class Program
    {
        /// <summary>
        /// app初始化
        /// </summary>

        // 创建日志
        const string logsFile = ("/opt/plcnext/apps/LiXinErFengAppLogs.txt");
        //const string logsFile = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\LiXinErFengAppLogs.txt";
        public static ILogNet logNet = new LogNetSingle(logsFile);

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
        static int clientNum = 4;  //一个EPC对应采集两个基恩士的数据（点表相同）  上位链路+MC协议，同时在线加起来不能超过15台
        public static KeyenceMcNet[] _mc = new KeyenceMcNet[clientNum];

        //创建三个线程            
        static int thrNum = 9;  //开启三个线程
        static Thread[] thr = new Thread[thrNum];

        //创建nodeID字典 (读取XML用）
        public static Dictionary<string, string> nodeidDictionary1;
        public static Dictionary<string, string> nodeidDictionary2;
        public static Dictionary<string, string> nodeidDictionary3;
        public static Dictionary<string, string> nodeidDictionary4;

        //读取Excel用
        static ReadExcel readExcel = new ReadExcel();

        #region 从Excel解析来的数据实例化 (4795 4794 4785)

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

        #region 从Excel解析来的数据实例化 (4752)

        //设备信息数据
        static DeviceInfoConSturct_MC[] StationMemory_LXEF2;

        //两个工位数据
        static StationInfoStruct_MC[] StationData1_LXEF2;
        static StationInfoStruct_MC[] StationData2_LXEF2;

        //1000ms 非报警信号
        static OneSecInfoStruct_MC[] OEE1_LXEF2;
        static OneSecInfoStruct_MC[] OEE2_LXEF2;
        static OneSecInfoStruct_MC[] Production_Data_LXEF2;
        static OneSecInfoStruct_MC[] Function_Enable_LXEF2;
        static OneSecInfoStruct_MC[] Life_Management_LXEF2;

        //报警信号
        static OneSecAlarmStruct_MC[] Alarm_LXEF2;

        #endregion

     
        #region 数据点位名和设备总览表格的实例化结构体

        //点位名
        static OneSecPointNameStruct_IEC PointNameStruct_IEC = new OneSecPointNameStruct_IEC();

        // 设备总览
        static DeviceInfoStruct_IEC[] deviceInfoStruct1_IEC;    //LXEFData(4795 4794 4785)
        static DeviceInfoStruct_IEC[] deviceInfoStruct2_IEC;    //LXEFData(4752)
        #endregion


        static void Main(string[] args)
        {

            int stepNumber = 10;

            while (true)
            {
                switch (stepNumber)
                {

                    case 10:
                        {
                            /// <summary>
                            /// 执行初始化
                            /// </summary>

                            logNet.WriteInfo(DateTime.Now.ToString() + "App Start");

                            #region 读取Excel （4795 4794 4785对应点表 LXEFData.xlsx； 4752对应点表 LXEFData(4752).xlsx）

                            string excelFilePath1 = Directory.GetCurrentDirectory() + "\\LXEFData.xlsx";
                            string excelFilePath2 = Directory.GetCurrentDirectory() + "\\LXEFData(4752).xlsx";     //PC端测试路径
                            
                            //string excelFilePath1 = "/opt/plcnext/apps/LXEFData.xlsx";
                            //string excelFilePath2 = "/opt/plcnext/apps/LXEFData(4752).xlsx";                         //EPC存放路径
                                                    
                            XSSFWorkbook excelWorkbook1 = readExcel.connectExcel(excelFilePath1);   // LXEFData(4795 4794 4785)
                            XSSFWorkbook excelWorkbook2 = readExcel.connectExcel(excelFilePath2);   // LXEFData(4752)  

                            Console.WriteLine("LXEFData read {0}", excelWorkbook1 != null ? "success" : "fail");
                            logNet.WriteInfo(DateTime.Now.ToString() + "  :LXEFData read ", excelWorkbook1 != null ? "success" : "fail");

                            Console.WriteLine("LXEFData(4752) read {0}", excelWorkbook2 != null ? "success" : "fail");
                            logNet.WriteInfo(DateTime.Now.ToString() + "  :LXEFData(4752) read ", excelWorkbook2 != null ? "success" : "fail");
                         
                            #endregion


                            #region 从xml获取nodeid，Grpc发送到对应变量时使用，注意xml中的别名要和对应类的属性名一致 

                            try
                            {
                                //EPC中存放的路径
                                const string filePath1 = "/opt/plcnext/apps/GrpcSubscribeNodes_4795.xml";

                                //PC中存放的路径                               
                                //const string filePath1 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4795.xml";  
                                
                                //将xml中的值写入字典中
                                nodeidDictionary1 = grpcToolInstance.getNodeIdDictionary(filePath1);

                                logNet.WriteInfo(DateTime.Now.ToString() + "  :NodeID Sheet 4795 read successfully");
                            }
                            catch(Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError(DateTime.Now.ToString() + "  :NodeID Sheet 4795 read failed");

                            }

                            try
                            {
                                //EPC中存放的路径
                                const string filePath2 = "/opt/plcnext/apps/GrpcSubscribeNodes_4794.xml";

                                //PC中存放的路径                               
                                 //const string filePath2 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4794.xml";  
                               
                                //将xml中的值写入字典中
                                nodeidDictionary2 = grpcToolInstance.getNodeIdDictionary(filePath2);

                                logNet.WriteInfo(DateTime.Now.ToString() + "  :NodeID Sheet 4794 read successfully");
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError(DateTime.Now.ToString() + "  :NodeID Sheet 4794 read failed");

                            }

                            try
                            {
                                //EPC中存放的路径
                                const string filePath3 = "/opt/plcnext/apps/GrpcSubscribeNodes_4785.xml";

                                //PC中存放的路径                               
                                //const string filePath3 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4785.xml";  

                                //将xml中的值写入字典中
                                nodeidDictionary3 = grpcToolInstance.getNodeIdDictionary(filePath3);

                                logNet.WriteInfo(DateTime.Now.ToString() + "  :NodeID Sheet 4785 read successfully");
                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError(DateTime.Now.ToString() + "  :NodeID Sheet 4785 read failed");

                            }

                            try
                            {
                                //EPC中存放的路径      
                                const string filePath4 = "/opt/plcnext/apps/GrpcSubscribeNodes_4752.xml";

                                //PC中存放的路径 
                                //const string filePath4 = "D:\\2024\\Work\\12-冠宇数采项目\\ReadFromStructArray\\LiXinErFeng_MC\\Ph_Mc_LiXinErFeng\\Ph_Mc_LiXinErFeng\\GrpcSubscribeNodes\\GrpcSubscribeNodes_4752.xml";  
                               
                                //将xml中的值写入字典中
                                nodeidDictionary4 = grpcToolInstance.getNodeIdDictionary(filePath4);

                                logNet.WriteInfo(DateTime.Now.ToString() + "  :NodeID Sheet 4752 read successfully");

                            }
                            catch (Exception e)
                            {
                                logNet.WriteError("Error:" + e);
                                logNet.WriteError(DateTime.Now.ToString() + "  :NodeID Sheet 4752 read failed");
                            }

                            #endregion


                            #region 将Excel里的值写入结构体数组中 (4795 4794 4785)

                            // 设备信息（100ms）
                            StationMemory_LXEF1 = readExcel.ReadOneDeviceInfoConSturctInfo_Excel(excelWorkbook1, "设备信息", "工位记忆（BOOL)");

                            // 两个工位（100ms)
                            StationData1_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(1A1B)");
                            StationData2_LXEF1 = readExcel.ReadStationInfo_Excel(excelWorkbook1, "加工工位(2A2B)");

                            // 非报警信号（1000ms）
                            OEE1_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "OEE(1)",false);
                            OEE2_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "OEE(2)", false);
                            Function_Enable_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "功能开关", false);
                            Production_Data_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "生产统计",false);
                            Life_Management_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook1, "寿命管理", false);

                            // 报警信号（1000ms)
                            Alarm_LXEF1 = readExcel.ReadOneSecAlarm_Excel(excelWorkbook1, "报警信号");

                            #endregion


                            #region 将Excel里的值写入结构体数组中 (4752)

                            // 设备信息（100ms）
                            StationMemory_LXEF2 = readExcel.ReadOneDeviceInfoConSturctInfo_Excel(excelWorkbook2, "设备信息", "工位记忆（BOOL)");

                            // 两个工位（100ms)
                            StationData1_LXEF2 = readExcel.ReadStationInfo_Excel(excelWorkbook2, "加工工位(1A1B)");
                            StationData2_LXEF2 = readExcel.ReadStationInfo_Excel(excelWorkbook2, "加工工位(2A2B)");

                            // 非报警信号（1000ms）
                            OEE1_LXEF2 = readExcel.ReadOneSecInfo_Excel(excelWorkbook2, "OEE(1)", false);
                            OEE2_LXEF2 = readExcel.ReadOneSecInfo_Excel(excelWorkbook2, "OEE(2)", false);
                            Function_Enable_LXEF2 = readExcel.ReadOneSecInfo_Excel(excelWorkbook2, "功能开关", false);
                            Production_Data_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook2, "生产统计", false);
                            Life_Management_LXEF1 = readExcel.ReadOneSecInfo_Excel(excelWorkbook2, "寿命管理", false);

                            // 报警信号（1000ms)
                            Alarm_LXEF1 = readExcel.ReadOneSecAlarm_Excel(excelWorkbook2, "报警信号");

                            #endregion


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

                         
                            #region 读取并发送两份Excel里的设备总览表

                            deviceInfoStruct1_IEC = readExcel.ReadDeviceInfo_Excel(excelWorkbook1, "离心二封设备总览");   // LXEFData(4795 4794 4785)
                            deviceInfoStruct2_IEC = readExcel.ReadDeviceInfo_Excel(excelWorkbook2, "离心二封设备总览");   // LXEFData(4752) 

                            var listWriteItem = new List<WriteItem>();
                            try
                            {                          
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct1_IEC[0]));
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary2["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct1_IEC[1]));
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary3["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct1_IEC[2]));
                                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary1["OverviewInfo"], Arp.Type.Grpc.CoreType.CtStruct, deviceInfoStruct2_IEC[0]));
                                
                                var writeItemsArray = listWriteItem.ToArray();
                                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                            
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine("ERRO: {0}", e);
                                logNet.WriteError(DateTime.Now.ToString() + "  OverviewInfo", e.ToString());
                            }

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

                            #region 发送4785的点位名 对应xml 为 nodeidDictionary3

                            keyenceClients.ReadandSendPointName(Production_Data_LXEF1, PointNameStruct_IEC, Production_Data_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1); //生产统计的点位名
                            keyenceClients.ReadandSendPointName(Function_Enable_LXEF1, PointNameStruct_IEC, Function_Enable_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1); //功能开关的点位名
                            keyenceClients.ReadandSendPointName(Life_Management_LXEF1, PointNameStruct_IEC, Life_Management_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1); //寿命管理的点位名
                            keyenceClients.ReadandSendPointName(Alarm_LXEF1, PointNameStruct_IEC, Alarm_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1);                     //报警信号的点位名

                            keyenceClients.ReadandSendPointName(StationData1_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1);        //加工工位一的点位名
                            keyenceClients.ReadandSendPointName(StationData2_LXEF1, PointNameStruct_IEC, StationData1_LXEF1.Length, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1);        //加工工位二的点位名

                            keyenceClients.ReadandSendPointName(OEEPointName, PointNameStruct_IEC, stringnumber, grpcToolInstance, nodeidDictionary3, grpcDataAccessServiceClient, options1);  //OEE的点位名

                            #endregion

                            # region 发送4752的点位名 对应xml 为 nodeidDictionary4

                            keyenceClients.ReadandSendPointName(Production_Data_LXEF2, PointNameStruct_IEC, Production_Data_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1); //生产统计的点位名
                            keyenceClients.ReadandSendPointName(Function_Enable_LXEF2, PointNameStruct_IEC, Function_Enable_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1); //功能开关的点位名
                            keyenceClients.ReadandSendPointName(Life_Management_LXEF2, PointNameStruct_IEC, Life_Management_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1); //寿命管理的点位名
                            keyenceClients.ReadandSendPointName(Alarm_LXEF2, PointNameStruct_IEC, Alarm_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1);                     //报警信号的点位名

                            keyenceClients.ReadandSendPointName(StationData1_LXEF2, PointNameStruct_IEC, StationData1_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1);        //加工工位一的点位名
                            keyenceClients.ReadandSendPointName(StationData2_LXEF2, PointNameStruct_IEC, StationData1_LXEF2.Length, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1);        //加工工位二的点位名

                            //将两个OEE的点位名拼成一个 string[]数组后，再发送 对应OEE表格
                            stringnumber = OEE1_LXEF2.Length + OEE2_LXEF2.Length;
                            OEEPointName = new string[stringnumber];
                            for (int i = 0; i < OEE1_LXEF2.Length; i++)
                            {
                                OEEPointName[i] = OEE1_LXEF2[i].varAnnotation;
                            }
                            for (int i = 0; i < OEE2_LXEF2.Length; i++)
                            {
                                OEEPointName[i + OEE1_LXEF2.Length] = OEE2_LXEF2[i].varAnnotation;
                            }
                            keyenceClients.ReadandSendPointName(OEEPointName, PointNameStruct_IEC, stringnumber, grpcToolInstance, nodeidDictionary4, grpcDataAccessServiceClient, options1);  //OEE的点位名

                            #endregion

                            logNet.WriteInfo(DateTime.Now.ToString() + "点位名发送完毕");


                            stepNumber = 20;
                        }

                        break;

                    case 20:
                        {
                            #region MC连接
                            
                            //_mc[0]:4795 _mc[1]:4794 _mc[2]:4785 _mc[3]:4752

                            for (int i = 0; i < clientNum; i++)
                            {
                                if(i< deviceInfoStruct1_IEC.Length)
                                {
                                    _mc[i] = new KeyenceMcNet(deviceInfoStruct1_IEC[i].strIPAddress, 5000);  //mc协议的端口号5000
                                    var retConnect = _mc[i].ConnectServer();
                                    Console.WriteLine("num {0} connect: {1})!", i, retConnect.IsSuccess ? "success" : "fail");
                                    logNet.WriteInfo("num " + i.ToString() + (retConnect.IsSuccess ? "success" : "fail"));
                                }
                                else
                                {
                                    _mc[i] = new KeyenceMcNet(deviceInfoStruct2_IEC[i].strIPAddress, 5000);  //mc协议的端口号5000
                                    var retConnect = _mc[i].ConnectServer();
                                    Console.WriteLine("num {0} connect: {1})!", i, retConnect.IsSuccess ? "success" : "fail");
                                    logNet.WriteInfo("num " + i.ToString() + (retConnect.IsSuccess ? "success" : "fail"));
                                }
                                
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

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo(StationMemory_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);                                   
                                    
                                    var ReadObject = "EM5057";
                                    ushort length = 25;
                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                                        Console.WriteLine(ReadObject + " Read Failed ");

                                    }


                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();
                  
                                    Console.WriteLine("No.4795 Thread 100ms Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            } );

                            // 1000ms数据
                            thr[1] = new Thread(() =>
                            {
                                var mc = _mc[0];
                                var nodeidDictionary = nodeidDictionary1;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //寿命管理
                                     keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                   
                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                  
                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);
                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];
                                    Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                    Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

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
                                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary1.GetValueOrDefault("OEE"));
                                        logNet.WriteError(DateTime.Now.ToString() + "  OEE Send Error:  " + e.ToString());
                                        
                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();
                                    
                                    Console.WriteLine("No.4795 Thread One Second Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
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

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo(StationMemory_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    var ReadObject = "EM5057";
                                    ushort length = 25;
                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                                        Console.WriteLine(ReadObject + " Read Failed ");

                                    }


                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4794 Thread 100ms Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });

                            // 1000ms数据
                            thr[3] = new Thread(() =>
                            {
                                var mc = _mc[1];
                                var nodeidDictionary = nodeidDictionary2;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //寿命管理
                                    keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);
                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];
                                    Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                    Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

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
                                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary1.GetValueOrDefault("OEE"));
                                        logNet.WriteError(DateTime.Now.ToString() + "  OEE Send Error:  " + e.ToString());

                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4794 Thread One Second Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });

                            #endregion

                            #region 编号4785

                            // 100ms数据
                            thr[4] = new Thread(() =>
                            {
                                var mc = _mc[2];
                                var nodeidDictionary = nodeidDictionary3;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo(StationMemory_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    var ReadObject = "EM5057";
                                    ushort length = 25;
                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF1, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                                        Console.WriteLine(ReadObject + " Read Failed ");

                                    }


                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4785 Thread 100ms Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });

                            // 1000ms数据
                            thr[5] = new Thread(() =>
                            {
                                var mc = _mc[2];
                                var nodeidDictionary = nodeidDictionary3;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //寿命管理
                                    keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF1, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF1, mc);
                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF1, mc);
                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];
                                    Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                    Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

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
                                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary1.GetValueOrDefault("OEE"));
                                        logNet.WriteError(DateTime.Now.ToString() + "  OEE Send Error:  " + e.ToString());

                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4785 Thread One Second Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });

                            #endregion

                            #region 编号4752
                            // 100ms数据
                            thr[6] = new Thread(() =>
                            {
                                var mc = _mc[3];
                                var nodeidDictionary = nodeidDictionary4;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    keyenceClients.ReadandSendDeviceInfo(StationMemory_LXEF2, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    var ReadObject = "EM5057";
                                    ushort length = 25;
                                    OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);
                                    if (ret.IsSuccess)
                                    {
                                        keyenceClients.SendStationData(StationData1_LXEF2, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                        keyenceClients.SendStationData(StationData2_LXEF2, ret.Content, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);
                                    }
                                    else
                                    {
                                        logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                                        Console.WriteLine(ReadObject + " Read Failed ");

                                    }


                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4752 Thread 100ms Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 100)
                                    {
                                        int sleepTime = 100 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });

                            // 1000ms数据
                            thr[7] = new Thread(() =>
                            {
                                var mc = _mc[3];
                                var nodeidDictionary = nodeidDictionary4;

                                while (true)
                                {
                                    TimeSpan start = new TimeSpan(DateTime.Now.Ticks);

                                    //功能开关
                                    keyenceClients.ReadandSendConOneSecData(Function_Enable_LXEF2, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //生产统计
                                    keyenceClients.ReadandSendConOneSecData(Production_Data_LXEF2, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //寿命管理
                                    keyenceClients.ReadandSendDisOneSecData(Life_Management_LXEF2, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //报警信号
                                    keyenceClients.ReadandSendAlarmData(Alarm_LXEF2, mc, grpcToolInstance, nodeidDictionary, grpcDataAccessServiceClient, options1);

                                    //OEE数据
                                    bool[] OEE_temp1 = keyenceClients.ReadOEEData(OEE1_LXEF2, mc);
                                    bool[] OEE_temp2 = keyenceClients.ReadOEEData(OEE2_LXEF2, mc);
                                    bool[] senddata = new bool[OEE_temp1.Length + OEE_temp2.Length];
                                    Array.Copy(OEE_temp1, 0, senddata, 0, OEE_temp1.Length);
                                    Array.Copy(OEE_temp2, 0, senddata, OEE_temp1.Length, OEE_temp2.Length);

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
                                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary1.GetValueOrDefault("OEE"));
                                        logNet.WriteError(DateTime.Now.ToString() + "  OEE Send Error:  " + e.ToString());

                                    }

                                    TimeSpan end = new TimeSpan(DateTime.Now.Ticks);
                                    DateTime nowDisplay = DateTime.Now;
                                    TimeSpan dur = (start - end).Duration();

                                    Console.WriteLine("No.4752 Thread One Second Data Read Time:{0} read Duration:{1}", nowDisplay.ToString("yyyy-MM-dd HH:mm:ss:fff"), dur.TotalMilliseconds);

                                    if (dur.TotalMilliseconds < 1000)
                                    {
                                        int sleepTime = 1000 - (int)dur.TotalMilliseconds;
                                        Thread.Sleep(sleepTime);
                                    }

                                }

                            });
                            #endregion



                            stepNumber = 100;

                        }
                        break;



                    case 100:
                        {
                            //开启线程
                            try
                            {
                                //  4795
                                thr[0].Start();  
                                thr[1].Start(); 

                                // 4794
                                thr[2].Start();
                                thr[3].Start();

                                // 4785
                                thr[4].Start();
                                thr[5].Start();

                                // 4752
                                thr[6].Start();
                                thr[7].Start();
                            }
                            catch
                            {
                                Console.WriteLine("Thread quit");
                                stepNumber = 1000;
                                break;

                            }

                            Thread.Sleep(100);

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