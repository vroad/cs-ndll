using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace cs
{
    class CSHandleContainer : IDisposable
    {
        private bool disposed = false;
        private Dictionary<string, int> sgNameToID;
        private List<string> sgIDToName;
        private LinkedList<GCHandle> persistentHandles;

        internal List<GCHandle> handles;
        internal List<IntPtr> memoryList;

        private static CSHandleContainer container;

        private CSHandleContainer()
        {
            sgNameToID = new Dictionary<string, int>();
            sgIDToName = new List<string>();
            persistentHandles = new LinkedList<GCHandle>();

            handles = new List<GCHandle>();
            memoryList = new List<IntPtr>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;

            for (int i = 0; i < handles.Count; ++i)
                handles[i].Free();

            for (LinkedListNode<GCHandle> it = persistentHandles.First; it != null; it = it.Next)
                it.Value.Free();

            for (int i = 0; i < memoryList.Count; ++i)
                Marshal.FreeHGlobal(memoryList[i]);

            disposed = true;
        }

        ~CSHandleContainer()
        {
            Dispose(false);
        }

        internal int GetId(string key)
        {
            if (sgNameToID.ContainsKey(key))
                return sgNameToID[key];
            int idx = sgIDToName.Count;
            sgIDToName.Add(key);
            sgNameToID.Add(key, idx);
            return idx;
        }

        internal string GetStringFromId(int id)
	    {
		    return sgIDToName[id];
	    }

        internal IntPtr CreateGCHandle(Object value)
        {
            handles.Add(GCHandle.Alloc(value, GCHandleType.Normal));
            return GCHandle.ToIntPtr(handles[handles.Count - 1]);
        }

        internal IntPtr CreatePersistentGCHandle(Object value)
        {
            if (value == null)
                return IntPtr.Zero;
            CSPersistent persistent = new CSPersistent(value);
            LinkedListNode<GCHandle> node = persistentHandles.AddLast(GCHandle.Alloc(persistent, GCHandleType.Normal));
            persistent.Node = node;
            return GCHandle.ToIntPtr(node.Value);
        }

        internal IntPtr CreatePinnedGCHandle(Object value)
        {
            handles.Add(GCHandle.Alloc(value, GCHandleType.Pinned));
            return GCHandle.ToIntPtr(handles[handles.Count - 1]);
        }

        internal IntPtr GetAddrOfBlittableObject(Object value)
        {
            handles.Add(GCHandle.Alloc(value, GCHandleType.Pinned));
            return handles[handles.Count - 1].AddrOfPinnedObject();
        }

        internal IntPtr AllocateMemory(int length)
        {
            IntPtr memory = Marshal.AllocHGlobal(length);
            memoryList.Add(memory);
            return memory;
        }

        internal void DestroyPersistentHandleWrap(CSPersistent persistent)
        {
            persistent.Node.Value.Free();
            persistentHandles.Remove(persistent.Node);
        }

        internal void ResizeHandles(int handleSize, int memoryListSize)
        {
            int oldHandleSize = handles.Count;
            for (int i = handleSize; i < oldHandleSize; ++i)
            {
                handles[i].Free();
            }
            handles.RemoveRange(handleSize, oldHandleSize - handleSize);

            int oldMemoryListSize = memoryList.Count;
            for (int i = memoryListSize; i < oldMemoryListSize; ++i)
                Marshal.FreeHGlobal(memoryList[i]);
            memoryList.RemoveRange(memoryListSize, oldMemoryListSize - memoryListSize);
        }

        internal static CSHandleContainer GetCurrent()
	    {
		    if (container == null)
			    container = new CSHandleContainer();
		
		    return container;
	    }
    
    }
}
