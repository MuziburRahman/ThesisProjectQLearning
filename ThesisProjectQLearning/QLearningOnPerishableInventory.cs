using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace ThesisProjectQLearning
{
    
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
        public const int LeadTime = 1;
        public int RemainingDaysToArrive { get; set; }
        public int Quantity { get; }
        public Order(int amount)
        {
            RemainingDaysToArrive = LeadTime;
            Quantity = amount;
        }
    }

    public class QLearningOnPerishableInventory
    {
        const int InventoryPositionCount_S2 = 5;//101;
        const int InventoryPositionCount_S1 = 21;
        const int L = 5; // product life
        const int RemainingLivesCount_S1 = InventoryPositionCount_S1 * L;
        const int OrderQuantitiesCount = 4;//41;
        const int a = 4;
        const int b = 5;
        const double SalePrice = 5.0;
        const double OutageCost = -6.0, ShortageCost = 2.0;
        const double OrderingCost = 3.0;

        const double LearningRate = 0.01;
        const int Episodes = 1000_000;
        const int MaxStepPerEpisode = 20;
        static double Epsilon = 1;
        const double EpsilonDecay = 0.998;
        const double FutureDiscount = 0.98;

        static int[] InventoryPositions2; // member of S2 

        static int[] InventoryPositions1; // member of S1
        static int[] RemainingLives;      // member of S1

        static int[] OrderQuantities;
        static List<Order> OrdersNotArrived;
        static List<Product> ProductsOnHand;

        public static readonly double[] EpisodeRewards = new double[Episodes];

        static QTable1 Table1;

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
            Table1 = new QTable1(InventoryPositions1, RemainingLives, OrderQuantities);
            Console.WriteLine("done initiating q table.");

            OrdersNotArrived = new List<Order>(OrderQuantitiesCount);
            ProductsOnHand = new List<Product>();

            var writer = File.CreateText("C:\\Users\\thisi\\OneDrive\\Desktop\\demands.txt");
            writer.Write(string.Join(',', MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)).Take(Episodes)));
            writer.Flush();
            writer.Dispose();
        }

        public static void Start()
        {
            var randm = new Random(DateTime.UtcNow.Millisecond);

            int life_rem = 0;
            for (int i = 0; i < 20; i++)
            {
                var p = new Product();
                ProductsOnHand.Add(p);
                life_rem += p.LifeRemaining;
            }

            int ep = 0;
            var itr = MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();
            var actual_demand = itr.Current;

            while (ep < Episodes)
            {
                double ep_reward = 0;
                double reward = 0.0;

                itr.MoveNext();
                var next_demand = itr.Current;

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    // recieve the arrived products that was ordered previously
                    for (int i = OrdersNotArrived.Count - 1; i >= 0; i--)
                    {
                        if (OrdersNotArrived[i].RemainingDaysToArrive == 0)
                        {
                            var n_new_product = OrdersNotArrived[i].Quantity;
                            for (int j = 0; j < n_new_product; j++)
                            {
                                var prdct = new Product();
                                prdct.LifeRemaining -= Order.LeadTime;
                                ProductsOnHand.Add(prdct);
                            }
                            OrdersNotArrived.RemoveAt(i);
                        }
                    }

                    // calculate shortage
                    int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);
                    reward += Ts * ShortageCost;

                    // remove the products consumed by customers
                    if (ProductsOnHand.Count > 0)
                    {
                        for (int i = 0; i < actual_demand; i++)
                        {
                            ProductsOnHand.RemoveAt(0);
                            reward += SalePrice;
                            if (ProductsOnHand.Count == 0)
                                break;
                        }
                    }

                    // discard outdated product and calculate outage amount
                    int To = 0;
                    life_rem = 0;
                    for (int i = ProductsOnHand.Count - 1; i >= 0; i--)
                    {
                        ProductsOnHand[i].LifeRemaining--;
                        if (ProductsOnHand[i].LifeRemaining <= 0)
                        {
                            To++;
                            ProductsOnHand.RemoveAt(i);
                        }
                        else
                            life_rem += ProductsOnHand[i].LifeRemaining;
                    }
                    reward += To * OutageCost;

                    ep_reward += reward;
                    if (ep_reward > 800)
                        break;

                    // determine order quantity
                    int oq, next_oq;
                    int total_product_count = ProductsOnHand.Count + OrdersNotArrived.Sum(o => o.Quantity);
                    if (randm.NextDouble() < Epsilon) // explore
                    {
                        oq = OrderQuantities[randm.Next(OrderQuantities.Length)];
                    }
                    else // exploit
                    {
                        oq = Table1.GetMaxOrderQuantityForState(total_product_count, life_rem, OrderQuantities.First(), OrderQuantities.Last());
                    }

                    // mustn't exceed inventory capacity
                    if (total_product_count + oq >= InventoryPositionCount_S1)
                        oq = InventoryPositionCount_S1 - 1 - total_product_count;

                    OrdersNotArrived.Add(new Order(oq));
                    reward = oq * OrderingCost;

                    for (int i = 0; i < OrdersNotArrived.Count; i++)
                    {
                        life_rem += L - (Order.LeadTime - OrdersNotArrived[i].RemainingDaysToArrive);
                        OrdersNotArrived[i].RemainingDaysToArrive--;
                    }

                    //double reward = -To * co - Ts * cs;
                    //Console.Write(reward.ToString("N2") + ",  ");
                    //if (ep % 20 == 0)
                    //    Console.WriteLine();

                    // calculate max q(s',a')
                    // quantity in next round = on hand inventory + orders in transit - next day demand
                    total_product_count = ProductsOnHand.Count + OrdersNotArrived.Sum(o => o.Quantity);
                    int qntity_next_round = total_product_count - next_demand;
                    next_oq = Table1.GetMaxOrderQuantityForState(total_product_count, life_rem, OrderQuantities.First(), OrderQuantities.Last());

                    // update q table
                    var state = new QuantityLifeState(total_product_count, life_rem);
                    var sa_pair = new QTableKey1(state, oq);
                    Table1[sa_pair] = (1 - LearningRate) * Table1[sa_pair] + LearningRate * (reward + FutureDiscount * next_oq);

                    actual_demand = next_demand;
                }

                EpisodeRewards[ep] = ep_reward;
                Console.WriteLine(ep_reward);

                ep_reward = 0;
                Epsilon *= EpsilonDecay;
                ep++;
            }
        }
    }
}
