using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Composer.Model
{
    public class ConcurrentList<T> : IList<T>
    {
        private object locker = new object();
        private List<T> list = new List<T>();

        private T GetAt(int index)
        {
            lock (locker)
            {
                return list[index];
            }
        }

        private void SetAt(int index, T t)
        {
            lock (locker)
            {
                list[index] = t;
            }
        }

        public T this[int index] { get =>  GetAt(index); set => SetAt(index, value); }

        private int GetCount()
        {
            lock (locker)
            {
                return list.Count();
            }
        }

        public int Count => GetCount();

        public bool IsReadOnly => false;

        public void Add(T t)
        {
            lock (locker)
            {
                list.Add(t);
            }
        }

        public void Clear()
        {
            lock (locker)
            {
                list.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (locker)
            {
                return list.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (locker)
            {
                list.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (locker)
            {
                return GetList().GetEnumerator();
            }
        }

        public List<T> GetList()
        {
            lock (locker)
            {
                return new List<T>(list);
            }
        }

        public int IndexOf(T item)
        {
            lock (locker)
            {
                return list.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (locker)
            {
                list.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (locker)
            {
                return list.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (locker)
            {
                list.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (locker)
            {
                return GetList().GetEnumerator();
            }
        }
    }
}
