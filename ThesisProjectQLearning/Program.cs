using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

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
    }

    public class Product
    {
        public int LifeRemaining { get; set; }

        public Product()
        {
            LifeRemaining = 5;
        }
    }
    
    public class Order
    {
        public int LeadTime { get; }
        public int RemainingDaysToArrive { get; set; }
        public int Quantity { get; }
        public Order(int amount, int lead_time)
        {
            LeadTime = lead_time;
            RemainingDaysToArrive = lead_time;
            Quantity = amount;
        }
    }

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
    }

    public class QTable2 : IDictionary<QTableKey2, double>
    {
        private Dictionary<QTableKey2, double> dict_internal;

        public double this[QTableKey2 key] 
        { 
            get { return dict_internal[key]; } 
            set { dict_internal[key] = value; } 
        }

        public ICollection<QTableKey2> Keys { get { return dict_internal.Keys; } }

        public ICollection<double> Values { get { return dict_internal.Values; } }

        public int Count { get { return dict_internal.Count; } }

        public bool IsReadOnly { get { return false; } }

        public QTable2(int[] inv_pos, int[] oq)
        {
            Random rnd = new Random(DateTime.UtcNow.Millisecond);
            var initial_q_values = new[] { 0, -5, 5, 10, -10 };

            for (int i = 0; i < inv_pos.Length; i++)
            {
                for (int k = 0; k < oq.Length; k++)
                {
                    double q_val = initial_q_values[rnd.Next(5)] + rnd.NextDouble();
                    Add(inv_pos[i], oq[k], q_val);
                }
            }
        }

        public void Add(QTableKey2 key, double value)
        {
            dict_internal.Add(key, value);
        }
        
        public void Add(int ip, int oq, double value)
        {
            var key = new QTableKey2(ip, oq);
            dict_internal.Add(key, value);
        }

        public void Add(KeyValuePair<QTableKey2, double> item)
        {
            dict_internal.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            dict_internal.Clear();
        }

        public bool Contains(KeyValuePair<QTableKey2, double> item)
        {
            return dict_internal.ContainsKey(item.Key) && dict_internal[item.Key] == item.Value;
        }

        public bool ContainsKey(QTableKey2 key)
        {
            return dict_internal.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<QTableKey2, double>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<QTableKey2, double>> GetEnumerator()
        {
            return dict_internal.GetEnumerator();
        }

        public bool Remove(QTableKey2 key)
        {
            return dict_internal.Remove(key);
        }

        /// <summary>
        /// removes if both key and value matches
        /// </summary>
        public bool Remove(KeyValuePair<QTableKey2, double> item)
        {
            if (!Contains(item))
                return false;
            return dict_internal.Remove(item.Key);
        }

        public bool TryGetValue(QTableKey2 key, [MaybeNullWhen(false)] out double value)
        {
            if(dict_internal.ContainsKey(key))
            {
                value = dict_internal[key];
                return true;
            }
            value = 0.0f;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dict_internal.GetEnumerator();
        }
    }

    class Program
    {
        const int InventoryPositionCount_S2 = 101;
        const int InventoryPositionCount_S1 = 21;
        const int L = 5; // product life
        const int RemainingLivesCount_S1 = InventoryPositionCount_S1 * L;
        const int OrderQuantitiesCount = 41;
        const int a = 4;
        const int b = 5;
        static double co = 1, cs = 2;

        const double LearningRate = 0.0001;
        const int Episodes = 100_000;
        static double Epsilon = 1;
        static double EpsilonDecay = 0.998;
        static double FutureDiscount = 0.98;

        static int[] InventoryPositions2; // member of S2 

        static int[] InventoryPositions1; // member of S1
        static int[] RemainingLives;      // member of S1

        static int[] OrderQuantities;

        static void Main(string[] args)
        {
            // initialization

            InventoryPositions2 = new int[InventoryPositionCount_S2];
            for (int i = 0; i < InventoryPositionCount_S2; i++)
            {
                InventoryPositions2[i] = i;
            }

            InventoryPositions1 = new int[InventoryPositionCount_S1];
            for (int i = 0; i < InventoryPositionCount_S1; i++)
            {
                InventoryPositions1[i] = i;
            }

            RemainingLives = new int[RemainingLivesCount_S1];
            for (int i = 0; i < RemainingLivesCount_S1; i++)
            {
                RemainingLives[i] = i;
            }

            OrderQuantities = new int[OrderQuantitiesCount];
            for (int i = 0; i < OrderQuantitiesCount; i++)
            {
                OrderQuantities[i] = i;
            }

            Console.WriteLine("initiating q table...");
            var table1 = new QTable1(InventoryPositions1, RemainingLives, OrderQuantities);
            var randm = new Random(DateTime.UtcNow.Millisecond);


            int life_rem = 0;
            List<Product> products_on_hand = new List<Product>();
            for (int i = 0; i < 20; i++)
            {
                var p = new Product();
                products_on_hand.Add(p);
                life_rem += p.LifeRemaining;
            }
            
            List<Order> orders_not_arrived = new List<Order>(OrderQuantitiesCount);

            int lead_time = 1;

            int ep = 0;
            foreach (var actual_demand in MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)))
            {
                // calculate shortage
                int Ts = Math.Max(0, actual_demand - products_on_hand.Count);

                // remove the products consumed by customers
                if(products_on_hand.Count > 0)
                {
                    for (int i = 0; i < actual_demand; i++)
                    {
                        products_on_hand.RemoveAt(0);
                        if (products_on_hand.Count == 0)
                            break;
                    }
                }

                // recieve the arrived products that was ordered previously
                for (int i = orders_not_arrived.Count - 1; i >= 0; i--)
                {
                    if(orders_not_arrived[i].RemainingDaysToArrive == 0)
                    {
                        var n_new_product = orders_not_arrived[i].Quantity;
                        for (int j = 0; j < n_new_product; j++)
                        {
                            var prdct = new Product();
                            prdct.LifeRemaining -= orders_not_arrived[i].LeadTime;
                            products_on_hand.Add(prdct);
                        }
                        orders_not_arrived.RemoveAt(i);
                    }
                }

                int oq, next_oq;

                if(randm.NextDouble() < Epsilon) // explore
                {
                    oq = OrderQuantities[randm.Next(OrderQuantities.Length)];
                }
                else // exploit
                {
                    oq = table1.GetMaxOrderQuantityForState(products_on_hand.Count, life_rem, OrderQuantities.First(), OrderQuantities.Last());
                }

                // mustn't exceed inventory capacity
                if (products_on_hand.Count + oq > InventoryPositionCount_S1)
                    oq = InventoryPositionCount_S1 - products_on_hand.Count;

                orders_not_arrived.Add(new Order(oq, lead_time));

                // calculate max q(s',a')



                // discard outdated product
                int To = 0;
                life_rem = 0;
                for (int i = products_on_hand.Count - 1; i >= 0; i--)
                {
                    products_on_hand[i].LifeRemaining--;
                    if (products_on_hand[i].LifeRemaining <= 0)
                    {
                        To++;
                        products_on_hand.RemoveAt(i);
                    }
                    else
                        life_rem += products_on_hand[i].LifeRemaining;
                }

                double reward = To * co + Ts * cs;

                Epsilon *= EpsilonDecay;

                ep++;
                if (ep > Episodes)
                    break;
            }
        }
    }
}
