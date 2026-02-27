using System;
using System.Collections.Generic;

namespace System.Collections.Generic
{
    // Minimal PriorityQueue implementation for older .NET versions
    public class PriorityQueue<TElement, TPriority>
    {
        private List<(TElement Element, TPriority Priority)> _heap = new List<(TElement, TPriority)>();
        private Comparer<TPriority> _comparer = Comparer<TPriority>.Default;

        public int Count => _heap.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add((element, priority));
            SiftUp(_heap.Count - 1);
        }

        public TElement Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("The queue is empty");
            var result = _heap[0].Element;
            var last = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            if (_heap.Count > 0)
            {
                _heap[0] = last;
                SiftDown(0);
            }
            return result;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_comparer.Compare(_heap[i].Priority, _heap[p].Priority) < 0)
                {
                    var tmp = _heap[i]; _heap[i] = _heap[p]; _heap[p] = tmp; i = p;
                }
                else break;
            }
        }

        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int l = 2 * i + 1;
                int r = 2 * i + 2;
                int smallest = i;
                if (l < n && _comparer.Compare(_heap[l].Priority, _heap[smallest].Priority) < 0) smallest = l;
                if (r < n && _comparer.Compare(_heap[r].Priority, _heap[smallest].Priority) < 0) smallest = r;
                if (smallest != i)
                {
                    var tmp = _heap[i]; _heap[i] = _heap[smallest]; _heap[smallest] = tmp; i = smallest;
                }
                else break;
            }
        }
    }
}
