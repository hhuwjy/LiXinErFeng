using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Omron;
using System.Threading;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Collections;
using Grpc.Core;
using static Arp.Plc.Gds.Services.Grpc.IDataAccessService;
using Arp.Plc.Gds.Services.Grpc;
using Grpc.Net.Client;
using static Ph_Mc_LiXinErFeng.GrpcTool;
using System.Net.Sockets;
using System.Drawing;
using Opc.Ua;
using NPOI.SS.Formula.Functions;
using HslCommunication.LogNet;
using Microsoft.Extensions.Logging;
using static Ph_Mc_LiXinErFeng.UserStruct;
using static Ph_Mc_LiXinErFeng.Program;
using HslCommunication.Profinet.LSIS;

using MathNet.Numerics.LinearAlgebra.Factorization;
using HslCommunication.Profinet.Keyence;
using System.Reflection;





namespace Ph_Mc_LiXinErFeng
{

    class KeyenceComm
    {


        //读取并发送设备信息 
        public void ReadandSendDeviceInfo(DeviceInfoConSturct_MC[] input, KeyenceMcNet mc, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            var ReadObject = input[0].varName;
            ushort length = (ushort)input.Length;
            var listWriteItem = new List<WriteItem>();          

            OperateResult<bool[]> ret = mc.ReadBool(ReadObject, length);

            if (ret.IsSuccess)
            {
                try
                {
                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary["DeviceInfo"], Arp.Type.Grpc.CoreType.CtArray, ret.Content));
                    var writeItemsArray = listWriteItem.ToArray();
                    var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                    bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault("DeviceInfo"));
                    logNet.WriteError(DateTime.Now.ToString() + "  DeviceInfo Send Error:  "+ e .ToString()  );
                   
                }
            }
            else
            {
                logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                Console.WriteLine( ReadObject + " Read Failed ");
                
            }
        }    

      

       //从大数组中取数，并发送工位数据
        public void SendStationData(StationInfoStruct_MC[] input, short[] EMArray, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            short[] senddata = new short[input.Length];
            string StationName_Now = CN2EN(input[0].stationName);
            var listWriteItem = new List<WriteItem>();

            for (int i = 0;i < input.Length;i++) 
            {
                var index = input[i].varOffset - 5057; //硬编码，开始地址就是5057
                senddata[i] = EMArray[index];  
            }

            try
            {
                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary[StationName_Now], Arp.Type.Grpc.CoreType.CtArray, senddata));
                var writeItemsArray = listWriteItem.ToArray();
                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
               
            }
            catch (Exception e)
            {
                Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault(StationName_Now));
                logNet.WriteError(DateTime.Now.ToString() +" "+ StationName_Now + " Send Error:  " + e.ToString());

               
            }

        }
    
  



        #region 读1000ms的数据

        //读取OEE数据
        public bool[] ReadOEEData(OneSecInfoStruct_MC[] input, KeyenceMcNet mc)
        {          
            var ReadObject = input[0].varName;
            ushort length = (ushort)input.Length;

            OperateResult<bool[]> ret = mc.ReadBool(ReadObject, length);
        
            if (ret.IsSuccess)
            {
                return ret.Content;
            }
            else
            {
                logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                Console.WriteLine(ReadObject + " Read Failed ");
                return null;
            }
        }
      
        //读取并发送 功能开关、生产统计数据 
        public void ReadandSendConOneSecData(OneSecInfoStruct_MC[] input, KeyenceMcNet mc, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            var ReadObject = input[0].varName;
            ushort length = (ushort)input.Length;
            var listWriteItem = new List<WriteItem>();

            if (input[0].varType == "BOOL")  //读功能开关
            {
                OperateResult<bool[]> ret = mc.ReadBool(ReadObject, length);

                if (ret.IsSuccess)
                {
                    try
                    {
                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary[ReadObject], Arp.Type.Grpc.CoreType.CtArray, ret.Content));
                        var writeItemsArray = listWriteItem.ToArray();
                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                        
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault(ReadObject));
                        logNet.WriteError(DateTime.Now.ToString() + " " + ReadObject + " Send Error:  " + e.ToString());
                       
                    }
                }
                else
                {
                    logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                    Console.WriteLine(ReadObject + " Read Failed ");
                    
                }
            }
            else   //读生产统计
            {
                OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);

                if (ret.IsSuccess)
                {
                    try
                    {
                        listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary[ReadObject], Arp.Type.Grpc.CoreType.CtArray, ret.Content));
                        var writeItemsArray = listWriteItem.ToArray();
                        var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                        bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                       
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault(ReadObject));
                        logNet.WriteError(DateTime.Now.ToString() + " " + ReadObject + " Send Error:  " + e.ToString());
                       
                    }
                }
                else
                {
                    logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                    Console.WriteLine(ReadObject + " Read Failed ");
                   
                }
            }
        }

        //读取并发送 寿命管理数据 （需要根据偏移地址进行筛选）
        public void ReadandSendDisOneSecData(OneSecInfoStruct_MC[] input, KeyenceMcNet mc, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            short[] senddata = new short[input.Length];
            var ReadObject = input[0].varName;
            ushort length = (ushort)input.Length;
            var listWriteItem = new List<WriteItem>();

            OperateResult<short[]> ret = mc.ReadInt16(ReadObject, length);

            if(ret.IsSuccess)
            {
                for (int i = 0; i < input.Length; i++)
                {
                    var index = input[i].varOffset - input[0].varOffset;
                    senddata[i] = ret.Content[index];
                }

                try
                {
                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary[ReadObject], Arp.Type.Grpc.CoreType.CtArray, senddata));
                    var writeItemsArray = listWriteItem.ToArray();
                    var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                    bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault(ReadObject));
                    logNet.WriteError(DateTime.Now.ToString() + " " + ReadObject + " Send Error:  " + e.ToString());
                    
                }
            }
            else
            {
                logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                Console.WriteLine(ReadObject + " Read Failed ");
              

            }
           
        }

        //读取并发送报警数据
        public void ReadandSendAlarmData(OneSecAlarmStruct_MC[] input, KeyenceMcNet mc, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            bool[] senddata = new bool[input.Length];
            var ReadObject = input[0].varName.Replace(".0", "");
            ushort length = (ushort)input.Length;
            var listWriteItem = new List<WriteItem>();
            bool temp;

            OperateResult<byte[]> ret = mc.Read(ReadObject, length);
            if (ret.IsSuccess)
            {
                for (int i = 0;  i < ret.Content.Length ; i++)
                {
                    temp = mc.ByteTransform.TransBool(ret.Content, 0);   // 每个bool 一个字节
                    senddata[i] = temp;
                }

                try
                {
                    listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary[ReadObject], Arp.Type.Grpc.CoreType.CtArray, senddata));
                    var writeItemsArray = listWriteItem.ToArray();
                    var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                    bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);
                  
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERRO: {0}，{1}", e, nodeidDictionary.GetValueOrDefault(ReadObject));
                    logNet.WriteError(DateTime.Now.ToString() + " " + ReadObject + " Send Error:  " + e.ToString());
                   
                }
            }
            else
            {
                logNet.WriteError(DateTime.Now.ToString() + ReadObject + " Read Failed ");
                Console.WriteLine(ReadObject + " Read Failed ");
              

            }
        }

        #endregion


        #region 读取并发送点位名
        public void ReadandSendPointName(OneSecInfoStruct_MC[] InputStruct, OneSecPointNameStruct_IEC functionEnableNameStruct_IEC, int IEC_Array_Number, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {           
            functionEnableNameStruct_IEC.iDataCount = InputStruct.Length;
            functionEnableNameStruct_IEC.stringArrData = new stringStruct[IEC_Array_Number];
            var listWriteItem = new List<WriteItem>();
           
            for (int i = 0; i < IEC_Array_Number; i++)
            {
                if (i < InputStruct.Length)
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = InputStruct[i].varAnnotation;
                }
                else
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = " ";
                }
            }
            try
            {

                // TO DO LIST 这里的点位名尚未添加 InputStruct[0].varAnnotation 
                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary.GetValueOrDefault(InputStruct[0].varAnnotation), Arp.Type.Grpc.CoreType.CtStruct, functionEnableNameStruct_IEC));
                var writeItemsArray = listWriteItem.ToArray();
                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);

            }
            catch (Exception e)
            {
                logNet.WriteError(DateTime.Now.ToString() + InputStruct[0].varAnnotation + "ERRO: {0}", e.ToString());
                Console.WriteLine("ERRO: {0}", e);
            }

        }

        public void ReadandSendPointName(OneSecAlarmStruct_MC[] InputStruct, OneSecPointNameStruct_IEC functionEnableNameStruct_IEC, int IEC_Array_Number, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            functionEnableNameStruct_IEC.iDataCount = InputStruct.Length;
            functionEnableNameStruct_IEC.stringArrData = new stringStruct[IEC_Array_Number];
            var listWriteItem = new List<WriteItem>();

            for (int i = 0; i < IEC_Array_Number; i++)
            {
                if (i < InputStruct.Length)
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = InputStruct[i].varAnnotation;
                }
                else
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = " ";
                }
            }
            try
            {

                // TO DO LIST 这里的点位名尚未添加 InputStruct[0].varAnnotation 
                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary.GetValueOrDefault(InputStruct[0].varAnnotation), Arp.Type.Grpc.CoreType.CtStruct, functionEnableNameStruct_IEC));
                var writeItemsArray = listWriteItem.ToArray();
                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);

            }
            catch (Exception e)
            {
                logNet.WriteError(DateTime.Now.ToString() + InputStruct[0].varAnnotation + "ERRO: {0}", e.ToString());
                Console.WriteLine("ERRO: {0}", e);
            }

        }

        public void ReadandSendPointName(StationInfoStruct_MC[] InputStruct, OneSecPointNameStruct_IEC functionEnableNameStruct_IEC, int IEC_Array_Number, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            functionEnableNameStruct_IEC.iDataCount = InputStruct.Length;
            functionEnableNameStruct_IEC.stringArrData = new stringStruct[IEC_Array_Number];
            var listWriteItem = new List<WriteItem>();

            for (int i = 0; i < IEC_Array_Number; i++)
            {
                if (i < InputStruct.Length)
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = InputStruct[i].varAnnotation;
                }
                else
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = " ";
                }
            }
            try
            {

                // TO DO LIST 这里的点位名尚未添加 InputStruct[0].varAnnotation 
                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary.GetValueOrDefault(InputStruct[0].varAnnotation), Arp.Type.Grpc.CoreType.CtStruct, functionEnableNameStruct_IEC));
                var writeItemsArray = listWriteItem.ToArray();
                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);

            }
            catch (Exception e)
            {
                logNet.WriteError(DateTime.Now.ToString() + InputStruct[0].varAnnotation + "ERRO: {0}", e.ToString());
                Console.WriteLine("ERRO: {0}", e);
            }

        }

        public void ReadandSendPointName(String[] InputString, OneSecPointNameStruct_IEC functionEnableNameStruct_IEC, int IEC_Array_Number, GrpcTool grpcToolInstance, Dictionary<string, string> nodeidDictionary, IDataAccessServiceClient grpcDataAccessServiceClient, CallOptions options1)
        {
            var listWriteItem = new List<WriteItem>();
            WriteItem[] writeItems = new WriteItem[] { };
            functionEnableNameStruct_IEC.iDataCount = InputString.Length;
            functionEnableNameStruct_IEC.stringArrData = new stringStruct[IEC_Array_Number];
            for (int i = 0; i < IEC_Array_Number; i++)
            {
                if (i < InputString.Length)
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = InputString[i];
                }
                else
                {
                    functionEnableNameStruct_IEC.stringArrData[i].str = " ";
                }
            }
            try
            {
                // TO DO LIST 这里的点位名尚未添加 InputStruct[0].varAnnotation 
                listWriteItem.Add(grpcToolInstance.CreatWriteItem(nodeidDictionary.GetValueOrDefault(InputString[0]), Arp.Type.Grpc.CoreType.CtStruct, functionEnableNameStruct_IEC));
                var writeItemsArray = listWriteItem.ToArray();
                var dataAccessServiceWriteRequest = grpcToolInstance.ServiceWriteRequestAddDatas(writeItemsArray);
                bool result = grpcToolInstance.WriteDataToDataAccessService(grpcDataAccessServiceClient, dataAccessServiceWriteRequest, new IDataAccessServiceWriteResponse(), options1);

            }
            catch (Exception e)
            {
                logNet.WriteError(DateTime.Now.ToString() + InputString[0] + "ERRO: {0}", e.ToString());
                //Console.WriteLine("ERRO: {0}", e);
            }

        }

        #endregion



        //根据首地址和偏移地址，得出数据在所读数据中的索引， 起始地址为21偏移地址为 4101   数组索引为 （41-21）*16 +（01 -00 ）
       //适用于  LR MR R的软件元
        public int CalculateIndex_H(int startaddress, int offset)
        {
            // ep: 偏移地址 205401 拆分成 2054 和 01
            var start_x = startaddress % 100; //取前半部分
            var start_y = startaddress / 100; //取后半部分

            var offset_x = offset % 100; //取前半部分
            var offset_y = offset / 100; //取后半部分

            int index = (offset_y - start_y) * 16 + (offset_x - start_x);

            return index;
        }


        //XML标签转换 工位结构体数组的工位名是中文，为了方便XML与字典对应，需要转化为英文
        private string CN2EN(string NameCN)
        {
            string NameEN = "";

            switch (NameCN)
            {
                case "加工工位(1A1B)":
                    NameEN = "1A1B";
                    break;

                case "加工工位(2A2B)":
                    NameEN = "2A2B";
                    break;

                default:
                    break;

            }

            return NameEN;

        }




    }
}

