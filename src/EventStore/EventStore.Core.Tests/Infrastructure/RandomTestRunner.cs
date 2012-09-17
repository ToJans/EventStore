﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Infrastructure
{
    public class RandomTestRunner
    {
        private readonly int _maxIterCnt;
        private readonly PairingHeap<RandTestQueueItem> _queue;
        
        private int _iter;
        private int _curLogicalTime;
        private int _globalMsgId;

        public RandomTestRunner(int maxIterCnt)
        {
            _maxIterCnt = maxIterCnt;
            _queue = new PairingHeap<RandTestQueueItem>(new GlobalQueueItemComparer());
        }

        public bool Run(IRandTestFinishCondition finishCondition, params IRandTestItemProcessor[] processors)
        {
            Ensure.NotNull(finishCondition, "finishCondition");

            while (++_iter <= _maxIterCnt && _queue.Count > 0)
            {
                var item = _queue.DeleteMin();
                _curLogicalTime = item.LogicalTime;
                foreach (var processor in processors)
                {
                    processor.Process(_iter, item);
                }

                finishCondition.Process(_iter, item);
                if (finishCondition.Done)
                    break;

                item.Bus.Publish(item.Message);
            }
            return finishCondition.Success;
        }

        public void Enqueue(IPEndPoint endPoint, Message message, IPublisher bus, int timeDelay = 1)
        {
            System.Diagnostics.Debug.Assert(timeDelay >= 1);
            _queue.Add(new RandTestQueueItem(_curLogicalTime + timeDelay, _globalMsgId++, endPoint, message, bus));
        }

        private class GlobalQueueItemComparer: IComparer<RandTestQueueItem>
        {
            public int Compare(RandTestQueueItem x, RandTestQueueItem y)
            {
                if (x.LogicalTime == y.LogicalTime)
                    return x.GlobalId - y.GlobalId;
                return x.LogicalTime - y.LogicalTime;
            }
        }
    }
}
