using QLearningOnPerishableInventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLearningOnPerishableInventory
{

    public class QLearningMultiProduct
    {
        const int InventoryPositionCount_S1 = 41;
        const int RemainingLivesCount_S1 = InventoryPositionCount_S1 * Product.LIFE_SPAN;
        const int OrderQuantitiesCount1 = InventoryPositionCount_S1;
        const int a = 4;
        const int b = 5;
        const double SalePrice = 2.0;
        const double HoldingCostPerDay = -0.1;
        const double LotSize = 5;
        const double OutageCost = -SalePrice, ShortageCost = -0.5f;
        const double OrderingCost = 3.0;

        static readonly double LearningRate = 0.01;
        const int Episodes = 1_000;
        const int MaxStepPerEpisode = InventoryPositionCount_S1;
        static readonly double EpsilonDecay = Math.Exp(Math.Log(0.1) / Episodes);
        const double FutureDiscount = 0.98;

        readonly int NumberOfProduct = 40;

        /// <summary>
        /// Total unit of all product must be less than this
        /// </summary>
        int MaxTotalInventory = 100;

        /// <summary>
        /// individually each product mustn't be greater than their MaxInventory level in unit
        /// </summary>
        int[] MaxInventory;

        static Random randm = new Random(DateTime.UtcNow.Millisecond);

        public QLearningMultiProduct()
        {
            MaxInventory = new int[NumberOfProduct];
            for (int i = 0; i < NumberOfProduct; i++)
            {
                MaxInventory[i] = 30 + randm.Next(70);
            }
        }

        public static double[] Start(IProgress<double> pr)
        {
            double[] EpisodeRewards = new double[Episodes];

            var OrderQuantities = new int[OrderQuantitiesCount1 + 1];
            var InventoryPositions = new int[InventoryPositionCount_S1 + 1];
            var RemainingLives = new int[RemainingLivesCount_S1 + 1];

            Task.WhenAll(
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i <= OrderQuantitiesCount1; i++)
                    {
                        OrderQuantities[i] = i;
                    }
                }),
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i <= InventoryPositionCount_S1; i++)
                    {
                        InventoryPositions[i] = i;
                    }
                }),
                Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i <= RemainingLivesCount_S1; i++)
                    {
                        RemainingLives[i] = i;
                    }
                })).Wait();

            var Table1 = new QTable1(InventoryPositions, RemainingLives, OrderQuantities);

            double Epsilon = 1;
            int ep = 0;
            var itr = MathFunctions.GetDemand(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();

            while (ep < Episodes)
            {
                double ep_reward = 0;
                double HoldingCost = 0;
                var ProductsOnHand = new List<Product>(InventoryPositionCount_S1);

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    itr.MoveNext();
                    var actual_demand = itr.Current;
                    double PrevHoldingCost = 0;

                    int life_rem = ProductsOnHand.Sum(p => Product.LIFE_SPAN - p.LifeSpent);

                    // determine order quantity
                    int oq;
                    int total_product_count = ProductsOnHand.Count;
                    int max_oq = InventoryPositionCount_S1 - total_product_count;
                    var dec_rnd = randm.NextDouble();

                    if (dec_rnd < Epsilon) // explore
                    {
                        oq = OrderQuantities[randm.Next(max_oq)];
                    }
                    else // exploit
                    {
                        var key = Table1.GetMaxOrderQuantityForState(total_product_count, life_rem, max_oq);
                        oq = key.Action;
                    }

                    // recieve the arrived products that was ordered previously
                    for (int i = oq - 1; i >= 0; i--)
                    {
                        ProductsOnHand.Add(new Product(0));
                    }

                    // calculate shortage
                    int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);

                    int actual_sale = 0;
                    // remove the products consumed by customers
                    if (ProductsOnHand.Count > 0)
                    {
                        if (actual_demand >= ProductsOnHand.Count)
                        {
                            actual_sale = ProductsOnHand.Count;
                            ProductsOnHand.Clear();
                        }
                        else
                        {
                            // removing the oldest products
                            ProductsOnHand.RemoveRange(0, actual_demand);
                            actual_sale = actual_demand;
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
                        {
                            PrevHoldingCost += HoldingCostPerDay;
                            new_life_rem += Product.LIFE_SPAN - ProductsOnHand[i].LifeSpent;
                        }
                    }

                    double reward = Ts * ShortageCost + To * OutageCost + HoldingCost + actual_sale * SalePrice;
                    HoldingCost = PrevHoldingCost;

                    ep_reward += reward;

                    // calculate max q(s',a')
                    // quantity in next round = on hand inventory + orders in transit - next day demand
                    var new_total_product_count = ProductsOnHand.Count;
                    var next_maxq_key = Table1.GetMaxOrderQuantityForState(new_total_product_count, new_life_rem, OrderQuantities.Last() - new_total_product_count);
                    var max_q_future = Table1[next_maxq_key];

                    // update q table
                    var state = new QuantityLifeState(total_product_count, life_rem);
                    var sa_pair = new QTableKey1(state, oq);
                    Table1[sa_pair] = (1 - LearningRate) * Table1[sa_pair] + LearningRate * (reward + FutureDiscount * max_q_future);
                }

                EpisodeRewards[ep] = ep_reward;
                Epsilon *= EpsilonDecay;
                //LearningRate *= EpsilonDecay;
                if (ep % 50 == 0)
                    pr.Report(ep * 100.0 / Episodes);
                ep++;
            }

            return EpisodeRewards;
        }
    }
}
