﻿using System;

namespace ThesisProjectQLearning
{
    public struct QuantityLifeState
    {
        public readonly int InvPosition;
        public readonly int RemainingLife;

        /// <param name="pos"></param>
        public QuantityLifeState(int pos, int lf)
        {
            InvPosition = pos;
            RemainingLife = lf;
        }
    }

    public struct QTableKey1
    {
        public readonly QuantityLifeState State;
        /// <summary>
        /// Order quantity
        /// </summary>
        public readonly int Action;

        /// <param name="actn">order quantity</param>
        public QTableKey1(QuantityLifeState state, int actn)
        {
            State = state;
            Action = actn;
        }

        public override string ToString()
        {
            return "pos: " + State.InvPosition.ToString() + " ,remlife: " + State.RemainingLife.ToString() + " ,oq: " + Action.ToString();
        }
    }

    public class QTable1 : QTableBase<QTableKey1>
    {
        public QTable1(int[] inv_pos, int[] rem_life, int[] oq) : base(inv_pos.Length * rem_life.Length * oq.Length)
        {
            Random rnd = new Random(DateTime.UtcNow.Millisecond);
            var initial_q_values = new[] { 0, -5, 5, 10, -10 };
            Maximum = -double.MaxValue;

            for (int i = 0; i < inv_pos.Length; i++)
            {
                for (int j = 0; j < rem_life.Length; j++)
                {
                    for (int k = 0; k < oq.Length; k++)
                    {
                        double q_val = initial_q_values[rnd.Next(5)] + rnd.NextDouble();
                        Add(inv_pos[i], rem_life[j], oq[k], q_val);
                    }
                }
            }
        }

        public void Add(int ip, int life, int oq, double value)
        {
            var key = new QTableKey1(new QuantityLifeState(ip, life), oq);
            dict_internal.Add(key, value);
            if (value > Maximum)
                Maximum = value;
        }

        public int GetMaxOrderQuantityForState(int quantity, int remlife, int min_oq, int max_oq)
        {
            double max_q_value = - double.MaxValue;
            int ret = 0;

            for (int i = min_oq; i <= max_oq; i++)
            {
                QTableKey1 key = new QTableKey1(new QuantityLifeState(quantity, remlife), i);
                double q = dict_internal[key];
                if (q > max_q_value)
                {
                    max_q_value = q;
                    ret = key.Action;
                }
            }

            return ret;
        }
    }

}
