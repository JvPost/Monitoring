﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace MonitoringWindowsService
{
    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(IPubSubMonitoringContract))]
    public interface IPubSubMonitoringService
    {
        [OperationContract(IsOneWay = false, IsInitiating = true)]
        void Subscribe();

        [OperationContract(IsOneWay = false, IsTerminating = true)]
        void UnSubscribe();

        [OperationContract(IsOneWay = false)] // reply message is to the monitor.
        void PublishMonitorMessage(string message);

        [OperationContract(IsOneWay = false)] // need to create reply message;
        void MonitoredApplicationHello();
    }

    public interface IPubSubMonitoringContract
    {
        [OperationContract(IsOneWay = true)]
        void PublishMonitorMessageRan(string message);
        
        [OperationContract(IsOneWay = true)]
        void PublishUnsubscribeMessage();

        [OperationContract(IsOneWay = true)]
        void PublishSubscribeMessage();

        [OperationContract(IsOneWay = true)]
        void ErrorOccured(string exceptionMessage);
    }
}
