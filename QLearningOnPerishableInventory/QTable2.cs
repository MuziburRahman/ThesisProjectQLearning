using System;
using System.Linq;

namespace QLearningOnPerishableInventory
{
    public struct QTableKey2
    {
        /// <summary>
        /// quantity
        /// </summary>
        public readonly int State;
        /// <summary>
        /// Order quantity
        /// </summary>
        public readonly int Action;

        public QTableKey2(int state, int action)
        {
            State = state;
            Action = action;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(State, Action);
        }

        public override string ToString()
        {
            return State.ToString() + "," + Action.ToString();
        }
    }

    public class QTable2 : QTableBase<QTableKey2>
    {
        public QTable2(int[] inv_pos, int[] oq) : base(inv_pos.Length * oq.Length)
        {
            Random rnd = new Random(DateTime.UtcNow.Millisecond);
            var initial_q_values = new[] { 0, -5, 5, 10, -10 };
            var max_inv_pos = inv_pos.Last();

            for (int i = 0; i < inv_pos.Length; i++)
            {
                var oq_max = Math.Min(max_inv_pos - i + 1, oq.Length);
                for (int k = 0; k < oq_max; k++)
                {
                    double q_val = initial_q_values[rnd.Next(5)] + rnd.NextDouble();
                    Add(inv_pos[i], oq[k], q_val);
                }
            }
        }


        public void Add(int ip, int oq, double value)
        {
            var key = new QTableKey2(ip, oq);
            dict_internal.Add(key, value);
            //if (value > Maximum)
            //    Maximum = value;
        }

        public QTableKey2 GetMaxOrderQuantityForState(int quantity, int max_oq)
        {
            double max_q_value = -double.MaxValue;
            QTableKey2 key_for_max_q = default;

            for (int i = 0; i <= max_oq; i++)
            {
                QTableKey2 key = new QTableKey2(quantity, i);
                double q = dict_internal[key];
                if (q > max_q_value)
                {
                    max_q_value = q;
                    key_for_max_q = key;
                }
            }

            return key_for_max_q;
        }
    }
}
