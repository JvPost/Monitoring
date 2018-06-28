﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Description;

using PubSubService;

namespace PubSubServiceHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Uri subscriptionAddress = new Uri("http://localhost:9000/PubSubService/");
            Uri publishingAddress = new Uri("net.tcp://localhost:9001/PubSubService/");


            ServiceHost selfHost = new ServiceHost(typeof(PubSubService.PubSubService));
            // Binding for handling subscriptions the subscriptions.
            WSDualHttpBinding subscriptionBinding = new WSDualHttpBinding();
            selfHost.AddServiceEndpoint(typeof(IPubSubService), subscriptionBinding, subscriptionAddress);

            // Binding for handling messages. We use NetTcpBinding because it's way faster.
            NetTcpBinding publishBinding = new NetTcpBinding(SecurityMode.None, false);
            selfHost.AddServiceEndpoint(typeof(IPubSubService), publishBinding, publishingAddress);
           

            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.HttpGetUrl = subscriptionAddress;
            selfHost.Description.Behaviors.Add(smb);
            selfHost.Open();

            Console.WriteLine($"The service is ready.");
            Console.WriteLine("Press [enter] to terminate.");
            Console.ReadLine();

            selfHost.Close();        }
    }
}