using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLearningOnPerishableInventory
{

    public class Product
    {
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
    //        return Quantity * (ProductLife - day_spent);
    //    }
    //}

    public class QLearningOnPerishableInventory
    {
        const int ProductLife = 5;
        const int InventoryPositionCount_S1 = 41;
        const int InventoryPositionCount_S2 = 101;
        const int RemainingLivesCount_S1 = InventoryPositionCount_S1 * ProductLife;
        const int OrderQuantitiesCount1 = InventoryPositionCount_S1;
        const int OrderQuantitiesCount2 = InventoryPositionCount_S2;
        const int a = 4;
        const int b = 5;
        const double SalePrice = 5.0;
        const double HoldingCostPerDay = 0.001;
        const double LotSize = 5;
        const double OutageCost = -2, ShortageCost = -0.5f;
        const double OrderingCost = 3.0;

        static readonly double LearningRate = 0.01;
        const int Episodes = 1_000;
        const int MaxStepPerEpisode = InventoryPositionCount_S1;
        static readonly double EpsilonDecay = Math.Exp(Math.Log(0.1) / Episodes);
        const double FutureDiscount = 0.98;

        int[] InventoryPositions2; // member of S2 

        int[] OrderQuantities2;

        public readonly double[] EpisodeRewards2 = new double[Episodes];

        QTable2 Table2;

        public QLearningOnPerishableInventory()
        {
            // initialization

            InventoryPositions2 = new int[InventoryPositionCount_S2 + 1];
            for (int i = 0; i <= InventoryPositionCount_S2; i++)
            {
                InventoryPositions2[i] = i;
            }



            
            OrderQuantities2 = new int[OrderQuantitiesCount2 + 1];
            for (int i = 0; i <= OrderQuantitiesCount2; i++)
            {
                OrderQuantities2[i] = i;
            }

            Table2 = new QTable2(InventoryPositions2, OrderQuantities2);
        }

        public static double[] Start1(IProgress<double> pr)
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

            var randm = new Random(DateTime.UtcNow.Millisecond);

            double Epsilon = 1;
            int ep = 0;
            var itr = MathFunctions.GetDemandByGammaDist(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();

            while (ep < Episodes)
            {
                double ep_reward = 0;
                var ProductsOnHand = new List<Product>(InventoryPositionCount_S1);

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    itr.MoveNext();
                    var actual_demand = itr.Current;

                    int life_rem = ProductsOnHand.Sum(p => ProductLife - p.LifeSpent);
                    
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

                    // remove the products consumed by customers
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
                        if (ProductsOnHand[i].LifeSpent > ProductLife)
                        {
                            To++;
                            ProductsOnHand.RemoveAt(i);
                        }
                        else
                            new_life_rem += ProductLife - ProductsOnHand[i].LifeSpent;
                    }

                    double reward = Ts * ShortageCost + To * OutageCost;

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
        
        public void Start2(IProgress<double> pr)
        {
            var ProductsOnHand = new List<Product>(InventoryPositionCount_S2);

            var randm = new Random(DateTime.UtcNow.Millisecond);
            double Epsilon = 1;

            int ep = 0;
            var itr = MathFunctions.GetDemandByGammaDist(a, b, new Random(DateTime.UtcNow.Millisecond)).GetEnumerator();

            while (ep < Episodes)
            {
                double ep_reward = 0;
                ProductsOnHand.Clear();

                for (int step = 0; step < MaxStepPerEpisode; step++)
                {
                    itr.MoveNext();
                    var actual_demand = itr.Current;
                    
                    // determine order quantity
                    int oq;
                    int total_product_count = ProductsOnHand.Count;
                    int max_oq = InventoryPositionCount_S2 - total_product_count;
                    var dec_rnd = randm.NextDouble();

                    if (dec_rnd < Epsilon) // explore
                    {
                        oq = OrderQuantities2[randm.Next(max_oq)];
                    }
                    else // exploit
                    {
                        var key = Table2.GetMaxOrderQuantityForState(total_product_count, max_oq);
                        oq = key.Action;
                    }

                    // recieve the arrived products that was ordered previously
                    for (int i = oq - 1; i >= 0; i--)
                    {
                        ProductsOnHand.Add(new Product(0));
                    }

                    // calculate shortage
                    int Ts = Math.Max(0, actual_demand - ProductsOnHand.Count);

                    // remove the products consumed by customers
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
                    for (int i = ProductsOnHand.Count - 1; i >= 0; i--)
                    {
                        ProductsOnHand[i].LifeSpent++;
                        if (ProductsOnHand[i].LifeSpent > ProductLife)
                        {
                            To++;
                            ProductsOnHand.RemoveAt(i);
                        }
                    }

                    double reward = Ts * ShortageCost + To * OutageCost;
                    ep_reward += reward;

                    // calculate max q(s',a')
                    // quantity in next round = on hand inventory + orders in transit - next day demand
                    var new_total_product_count = ProductsOnHand.Count;
                    var next_maxq_key = Table2.GetMaxOrderQuantityForState(new_total_product_count, OrderQuantities2.Last() - new_total_product_count);
                    var max_q_future = Table2[next_maxq_key];

                    // update q table
                    var state = total_product_count;
                    var sa_pair = new QTableKey2(state, oq);
                    Table2[sa_pair] = (1 - LearningRate) * Table2[sa_pair] + LearningRate * (reward + FutureDiscount * max_q_future);
                }

                EpisodeRewards2[ep] = ep_reward;
                Epsilon *= EpsilonDecay;
                //LearningRate *= EpsilonDecay;
                if (ep % 50 == 0)
                    pr.Report(ep * 100.0 / Episodes);
                ep++;
            }

        }
    }
}
