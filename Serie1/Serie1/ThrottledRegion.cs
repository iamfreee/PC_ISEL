﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie1 {

    class Region {

        private int _inside;
        private int _maxWaiting;
        private int _timeout;
        public LinkedList<bool> WaitingQueue = new LinkedList<bool>();
        private readonly object _lock = new object();

        public Region(int maxInside, int maxWaiting, int timeout) {
            this._inside = maxInside;
            this._maxWaiting = maxWaiting;
            this._timeout = timeout;
        }

        public void Enter() {
            this._inside--;
            Debug.Assert(this._inside >= 0); // should never occur
        }

        public void Leave() {
            this._inside++;
        }

        public bool IsFullInside() {
            return _inside == 0;
        }

        public bool IsEmptyInside(int maxValue) {
            return _inside == maxValue;
        }

        public bool IsWaitingQueueFull() {
            return this.WaitingQueue.Count >= _maxWaiting;
        }

    }

    public class ThrottledRegion {

        private readonly int _maxInside;
        private readonly int _maxWaiting;
        private readonly int _waitTimeout;

        //signaling
        private readonly object _lock = new object(); // global lock
        private Dictionary<int, Region> regions = new Dictionary<int, Region>(); // all regions


        public ThrottledRegion(int maxInside, int maxWaiting, int waitTimeout) {
            _maxInside = maxInside;
            _maxWaiting = maxWaiting;
            _waitTimeout = waitTimeout;
        }


        // throws ThreadInterruptedException
        public bool TryEnter(int key) {
            lock (_lock) {
                Region region = null;
                if (regions.TryGetValue(key, out region)) { // we have the region
                    return EnterRegion(region);
                }

                // we must create the region
                region = new Region(_maxInside, _maxWaiting, _waitTimeout);
                regions.Add(key, region);

                return EnterRegion(region);
            }
        }

        public void Leave(int key) {
            lock (_lock) {
                Region region = null;
                if (regions.TryGetValue(key, out region) == false) {
                    return; // wtf? we must have the region!!
                }

                if (!region.IsEmptyInside(_maxInside)) { // region is empty inside?
                    region.Leave();
                }

                LinkedListNode<bool> first = region.WaitingQueue.First;
                if (first != null) { // someone is waiting?
                    first.Value = true; // green light to go when wake up
                    SyncUtils.Notify(_lock, first);
                }
            }
        }

        /*
        |--------------------------------------------------------------------------
		| Logic
		|--------------------------------------------------------------------------
		*/
        // We notify when leaving the waiting queue because we can lose 
        // notifications when two Leaves occur
        private bool EnterRegion(Region region) {
            if (region.IsFullInside() == false) {
                region.Enter();
                return true;
            }

            // We have the inside region full, we have to wait

            if (region.IsWaitingQueueFull()) {
                return false; //no space to wait, sorry bro
            }

            var node = region.WaitingQueue.AddLast(false);
            int timeout = _waitTimeout;
            int lastTime = (timeout != Timeout.Infinite) ? Environment.TickCount : 0;

            do {
                try {
                    SyncUtils.Wait(_lock, node, timeout);
                } catch (ThreadInterruptedException e) {
                    region.WaitingQueue.Remove(node);

                    throw;
                }

                if (node.Value) { // green light?
                    region.WaitingQueue.Remove(node);
                    region.Enter();

                    return true;
                }

                // out of time?
                if (SyncUtils.AdjustTimeout(ref lastTime, ref timeout) == 0) {
                    region.WaitingQueue.Remove(node);
                    return false;
                }
            } while (true);
        }
    }

}