﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lynx.Core.Services.Interfaces;
using Lynx.Core.Services;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Java.Lang;

namespace CoreUnitTests
{
    [TestFixture]
    public class SessionTest 
    {
        private ISession _session1;
        private ISession _session2;

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestSendAndReceiveMessage()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            string messageReceived = "";
            string message = "This is a test message";

            _session1 = new PubnubSession(delegate{});
            _session2 = new PubnubSession(
                delegate(object sender, string eventArgs)
                {
                    resetEvent.Set();
                    messageReceived = eventArgs;
                }
            );
;
            string sessionKey = _session1.Open();
            _session2.Open(sessionKey);

            Thread.Sleep(2000);

            _session1.Send(message);
            resetEvent.WaitOne(5000);

            Assert.AreEqual(message, messageReceived);
        }

    }
}
