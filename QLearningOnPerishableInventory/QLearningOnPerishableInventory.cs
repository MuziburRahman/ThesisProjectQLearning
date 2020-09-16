using System;
using System.Collections.Generic;
using System.Linq;

namespace QLearningOnPerishableInventory
{

    public class Product
    {
        public const int LIFE_SPAN = 5; // product life

        public int LifeSpent;

        public Product(int spent)
        {
            LifeSpent = spent;
        }
    }

    //public class Order
    //{
    //    public const int LeadTime = 1;
    //    public int RemainingDaysToArrive { get; set; }
    //    public int Quantity { get; }

    //    public Order(int amount)
    //    {
    //        RemainingDaysToArrive = LeadTime;
    //        Quantity = amount;
    //    }

    //    public int TotalRemainingLife()
    //    {
    //        int day_spent = LeadTime - RemainingDaysToArrive;
    //        return Quantity * (Product.LIFE_SPAN - day_spent);
    //    }
    //}

    public class QLearningOnPerishableInventory
    {
        const int InventoryPositionCount_S2 = 51;
        const int InventoryPositionCount_S1 = 41;
        const int RemainingLivesCount_S1 = InventoryPositionCount_S1 * Product.LIFE_SPAN;
        const int OrderQuantitiesCount = 41;
        const int a = 4;
        const int b = 5;
        const double SalePrice = 5.0;
        const double OutageCost = -2, ShortageCost = -0.5f;
        const double OrderingCost = 3.0;

        static double LearningRate = 0.01;
        const int Episodes = 5_000;
        const int MaxStepPerEpisode = 25;
        double Epsilon = 1;
        const double EpsilonDecay = 0.95998;
        const double FutureDiscount = 0.98;

        int[] InventoryPositions2; // member of S2 

        int[] InventoryPositions1; // member of S1
        int[] RemainingLives;      // member of S1

        int[] OrderQuantities;
        //List<Order> OrdersNotArrived;
        List<Product> ProductsOnHand;

        public readonly double[] EpisodeRewards = new double[Episodes];

        QTable1 Table1;
        QTable2 Table2;

        public QLearningOnPerishableInventory()
        {
            // initialization

            InventoryPositions2 = new int[InventoryPositionCount_S2 + 1];
            for (int i = 0; i <= InventoryPositionCount_S2; i++)
            {
                InventoryPositions2[i] = i;
            }

            InventoryPositions1 = new int[InventoryPositionCount_S1 + 1];
            for (int i = 0; i <= InventoryPositionCount_S1; i++)
            {
                InventoryPositions1[i] = i;
            }

            RemainingLives = new int[RemainingLivesCount_S1 + 1];
            for (int i = 0; i <= RemainingLivesCount_S1; i++)
            {
                RemainingLives[i] = i;
            }

            OrderQuantities = new int[OrderQuantitiesCount + 1];
            for (int i = 0; i <= OrderQuantitiesCount; i++)
            {
                OrderQuantities[i] = i;
            }

            Console.WriteLine("initiating q table...");
            Table1 = new QTable1(InventoryPositions1, RemainingLives, OrderQuantities);
            //Table2 = new QTable2(InventoryPositions2, OrderQuantities);
            Console.WriteLine("done initiating q table.");

            //OrdersNotArrived = new List<Order>(OrderQuantitiesCount);
            ProductsOnHand = new List<Product>();

        }

        public void Start1(IProgress<double> pr)
        {
            var randm = new Random(DateTime.UtcNow.Millisecond);

            int ep = 0;
            var itr = MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();

            while (ep < Episodes)
            {
                double ep_reward = 0;
                ProductsOnHand.Clear();
                //OrdersNotArrived.Clear();

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    itr.MoveNext();
                    var actual_demand = itr.Current;

                    int life_rem = ProductsOnHand.Sum(p => Product.LIFE_SPAN - p.LifeSpent);
                    
                    // determine order quantity
                    int oq;
                    int total_product_count = ProductsOnHand.Count;
                    int max_oq = InventoryPositionCount_S1 - total_product_count;
                    var dec_rnd = randm.NextDouble();

                    if (dec_rnd < Epsilon) // explore
                    {
                        oq = OrderQuantities[randm.Next(max_oq)];
                        //System.Diagnostics.Debug.WriteLine("random oq = " + oq.ToString());
                    }
                    else // exploit
                    {
                        var key = Table1.GetMaxOrderQuantityForState(total_product_count, life_rem, OrderQuantities.First(), max_oq);
                        oq = key.Action;
                        //System.Diagnostics.Debug.WriteLine("oq = " + oq.ToString() + " at " + total_product_count.ToString());
                    }

                    //int not_arr_oq = 0;

                    // recieve the arrived products that was ordered previously
                    for (int i = oq - 1; i >= 0; i--)
                    {
                        ProductsOnHand.Add(new Product(0));
                    }

                    // calculate shortage
                    int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);

                    // remove the products consumed by customers
                    //System.Diagnostics.Debug.WriteLine("demand = " + actual_demand.ToString());
                    if (ProductsOnHand.Count > 0)
                    {
                        if(actual_demand >= ProductsOnHand.Count)
                        {
                            ProductsOnHand.Clear();
                        }
                        else
                        {
                            // removing the oldest products
                            ProductsOnHand.RemoveRange(0, actual_demand);
                        }
                    }

                    // discard outdated product and calculate outage amount
                    int To = 0;
                    var new_life_rem = 0;
                    for (int i = ProductsOnHand.Count - 1; i >= 0; i--)
                    {
                        ProductsOnHand[i].LifeSpent++;
                        if (ProductsOnHand[i].LifeSpent > Product.LIFE_SPAN)
                        {
                            To++;
                            ProductsOnHand.RemoveAt(i);
                        }
                        else
                            new_life_rem += Product.LIFE_SPAN - ProductsOnHand[i].LifeSpent;
                    }

                    double reward = Ts * ShortageCost + To * OutageCost;

                    if (reward >= 0)
                    {
                        reward = 10;
                    }
                    ep_reward += reward;

                    //OrdersNotArrived.Add(new Order(oq));
                    //if (step < Order.LeadTime)
                    //    continue;

                    //not_arr_oq += oq;

                    // calculate max q(s',a')
                    // quantity in next round = on hand inventory + orders in transit - next day demand
                    var new_total_product_count = ProductsOnHand.Count;
                    var next_maxq_key = Table1.GetMaxOrderQuantityForState(new_total_product_count, new_life_rem, OrderQuantities.First(), OrderQuantities.Last() - new_total_product_count);
                    var max_q_future = Table1[next_maxq_key];

                    // update q table
                    var state = new QuantityLifeState(total_product_count, life_rem);
                    var sa_pair = new QTableKey1(state, oq);
                    Table1[sa_pair] = (1 - LearningRate) * Table1[sa_pair] + LearningRate * (reward + FutureDiscount * max_q_future);
                }

                EpisodeRewards[ep] = ep_reward;
                Epsilon *= EpsilonDecay;
                //LearningRate *= EpsilonDecay;
                if (ep % 100 == 0)
                    pr.Report(ep * 100.0 / Episodes);
                ep++;
            }

        }

        //public void Start2(IProgress<double> pr)
        //{
        //    var randm = new Random(DateTime.UtcNow.Millisecond);

        //    for (int i = 0; i < 20; i++)
        //    {
        //        var p = new Product(0);
        //        ProductsOnHand.Add(p);
        //    }

        //    int ep = 0;
        //    var itr = MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();
        //    var actual_demand = itr.Current;

        //    while (ep < Episodes)
        //    {
        //        double ep_reward = 0;
        //        double reward = 0.0;

        //        itr.MoveNext();
        //        var next_demand = itr.Current;

        //        for (int step = 0; step < MaxStepPerEpisode; step++)
        //        {
        //            // recieve the arrived products that was ordered previously
        //            for (int i = OrdersNotArrived.Count - 1; i >= 0; i--)
        //            {
        //                if (OrdersNotArrived[i].RemainingDaysToArrive == 0)
        //                {
        //                    var n_new_product = OrdersNotArrived[i].Quantity;
        //                    for (int j = 0; j < n_new_product; j++)
        //                    {
        //                        var prdct = new Product(0);
        //                        ProductsOnHand.Add(prdct);
        //                    }
        //                    OrdersNotArrived.RemoveAt(i);
        //                }
        //            }

        //            // calculate shortage
        //            int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);
        //            reward += Ts * ShortageCost;

        //            // remove the products consumed by customers
        //            if (ProductsOnHand.Count > 0)
        //            {
        //                for (int i = 0; i < actual_demand; i++)
        //                {
        //                    ProductsOnHand.RemoveAt(0);
        //                    //reward += SalePrice;
        //                    if (ProductsOnHand.Count == 0)
        //                        break;
        //                }
        //            }

        //            // discard outdated product and calculate outage amount
        //            int To = 0;
        //            for (int i = ProductsOnHand.Count - 1; i >= 0; i--)
        //            {
        //                ProductsOnHand[i] = new Product(ProductsOnHand[i].LifeSpent + 1);
        //                if (ProductsOnHand[i].LifeSpent > Product.LIFE_SPAN)
        //                {
        //                    To++;
        //                    ProductsOnHand.RemoveAt(i);
        //                }
        //            }
        //            reward += To * OutageCost;

        //            ep_reward += reward;
        //            if (ep_reward > 800)
        //                break;

        //            // determine order quantity
        //            int oq, next_oq;
        //            int total_product_count = ProductsOnHand.Count + OrdersNotArrived.Sum(o => o.Quantity);
        //            if (randm.NextDouble() < Epsilon) // explore
        //            {
        //                oq = OrderQuantities[randm.Next(OrderQuantities.Length)];
        //            }
        //            else // exploit
        //            {
        //                oq = Table2.GetMaxOrderQuantityForState(total_product_count, OrderQuantities.First(), OrderQuantities.Last());
        //            }

        //            // mustn't exceed inventory capacity
        //            int probable_oq = OrdersNotArrived.Sum(o => o.Quantity) + oq;
        //            if (total_product_count + probable_oq >= InventoryPositionCount_S2)
        //                oq = InventoryPositionCount_S2 - 1 - total_product_count - probable_oq;

        //            OrdersNotArrived.Add(new Order(oq));
        //            //reward = oq * OrderingCost;

        //            for (int i = 0; i < OrdersNotArrived.Count; i++)
        //            {
        //                OrdersNotArrived[i].RemainingDaysToArrive--;
        //            }

        //            //double reward = -To * co - Ts * cs;
        //            //Console.Write(reward.ToString("N2") + ",  ");
        //            //if (ep % 20 == 0)
        //            //    Console.WriteLine();

        //            // calculate max q(s',a')
        //            // quantity in next round = on hand inventory + orders in transit - next day demand
        //            total_product_count = ProductsOnHand.Count + OrdersNotArrived.Sum(o => o.Quantity);
        //            int qntity_next_round = total_product_count - next_demand;
        //            next_oq = Table2.GetMaxOrderQuantityForState(total_product_count, OrderQuantities.First(), OrderQuantities.Last());

        //            // update q table
        //            var sa_pair = new QTableKey2(total_product_count, oq);
        //            Table2[sa_pair] = (1 - LearningRate) * Table2[sa_pair] + LearningRate * (reward + FutureDiscount * next_oq);

        //            actual_demand = next_demand;
        //        }

        //        EpisodeRewards[ep] = ep_reward;
        //        Console.WriteLine(ep_reward);

        //        ep_reward = 0;
        //        Epsilon *= EpsilonDecay;
        //        if (ep % 100 == 0)
        //            pr.Report(ep * 100.0 / Episodes);
        //        ep++;
        //    }
        //}
    }
}
