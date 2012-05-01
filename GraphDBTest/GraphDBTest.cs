using System;
using System.Collections.Generic;
using System.IO;
using sones.GraphDB;
using sones.GraphDB.Request;
using sones.GraphDB.TypeSystem;
using sones.GraphDS.PluginManager;
using sones.GraphDSServer;
using sones.GraphQL.Result;
using sones.Library.Commons.Security;
using sones.Library.PropertyHyperGraph;
using sones.Library.VersionedPluginManager;
using sones.GraphQL;
using System.Net;

namespace GraphDBTest
{
    class GraphDBTest
    {
        //Экземпляр сервера базы даных
        IGraphDSServer GraphDSServer;

        //Токен безопасности и идентификатор транзакции
        SecurityToken SecToken;
        Int64 TransactionID;

        //Количество вершин и ребер
        int vertices, edges;
        
        //Список смежности
        List< List< KeyValuePair<long, Int32> > > edge_list;

        /// <summary> Конструктор класса GraphDBTest
        /// Создается база данных graphDB и сервер GraphDSServer
        /// </summary>
        public GraphDBTest()
        {
            var graphDB = new SonesGraphDB();

            List<PluginDefinition> QueryLanguagePATH = new List<PluginDefinition>();
            Dictionary<string, object> GQL_Parameters = new Dictionary<string, object>();
            GQL_Parameters.Add("GraphDB", graphDB);

            QueryLanguagePATH.Add(new PluginDefinition("sones.gql", GQL_Parameters));
 
            //adding the QueryLanguage as a GraphDSPlugin
            GraphDSPlugins PluginsAndParameters = new GraphDSPlugins(QueryLanguagePATH);

            GraphDSServer = new GraphDS_Server(graphDB, PluginsAndParameters);
            
            SecToken = GraphDSServer.LogOn(new UserPasswordCredentials("User", "test"));
            TransactionID = GraphDSServer.BeginTransaction(SecToken);

            edge_list = new List<List< KeyValuePair<long, Int32> >>();

            Dictionary<string, object> RestParameter = new Dictionary<string, object>();
            RestParameter.Add("IPAddress", IPAddress.Any);
            RestParameter.Add("Port", 9975);
            RestParameter.Add("Username", "test");
            RestParameter.Add("Password", "test");
            GraphDSServer.StartService("sones.RESTService", RestParameter);
        }

        /// <summary> Чистит всю базу данных
        /// 
        /// </summary>
        private void ClearGraphDB()
        {
            GraphDSServer.Clear<IRequestStatistics>(SecToken, TransactionID, new RequestClear(), (Statistics, DeletedTypes) => Statistics);
        }

        /// <summary> Считывает данные из входного файла
        /// n - количество вершин;
        /// m - количество ребер;
        /// дальше считываются ребра в список смежности
        /// </summary>
        private void ReadFile()
        {
            string path = "netData.txt";
            using (StreamReader sr = new StreamReader(path))
            {
                vertices = Int32.Parse(sr.ReadLine());
                edges = Int32.Parse(sr.ReadLine());
                for (int i = 0; i < vertices; ++i)
                    edge_list.Add(new List< KeyValuePair<long, Int32> >());

                for (int i = 0; i < edges; ++i)
                {
                    string[] input_edge = sr.ReadLine().Split();
                    int a = Int32.Parse(input_edge[0]);
                    int b = Int32.Parse(input_edge[1]);
                    int c = Int32.Parse(input_edge[2]);
                    //DateTime date = DateTime.Parse(input_edge[2]);

                    edge_list[a].Add( new KeyValuePair<long, Int32>(b, c) );
                    edge_list[b].Add(new KeyValuePair<long, Int32>(a, c));
                }
            }
        }

        /// <summary>
        /// Анализирует результат запроса в базу данных. Показывает ошибки, которые были во время запроса.
        /// </summary>
        /// <param name="myQueryResult">Результат запроса.</param>
        private bool CheckResult(IQueryResult myQueryResult)
        {
            if (myQueryResult.Error != null)
            {
                if (myQueryResult.Error.InnerException != null)
                    Console.WriteLine(myQueryResult.Error.InnerException.Message);
                else
                    Console.WriteLine(myQueryResult.Error.Message);

                return false;
            }
            else
            {
                Console.WriteLine("Query " + myQueryResult.TypeOfResult);

                return true;
            }
        }

        private void PrintWays(IVertexView current_vertex, int lev = 0)
        {
            for (int i = 0; i < 2 * lev; ++i)
                Console.Write(' ');
            if (current_vertex.HasProperty("VertexID"))
                Console.WriteLine(current_vertex.GetPropertyAsString("VertexID"));
            if (!current_vertex.HasEdge("path"))
                return;
            foreach (var edge in current_vertex.GetHyperEdge("path").GetAllEdges())
            {
                PrintWays(edge.GetTargetVertex(), lev + 1);
            }
        }

        private void PrintAllVerticesInBase(IVertexType vertex_type)
        {
            var vertices1 = GraphDSServer.GetVertices<IEnumerable<IVertex>>(SecToken,
                                                               TransactionID,
                                                               new RequestGetVertices("User"),
                                                               (statistics, Vertices) => Vertices);

            var attrName = vertex_type.GetAttributeDefinition("Name");
            var attrFriendsDate = vertex_type.GetOutgoingEdgeDefinition("friends").InnerEdgeType.GetAttributeDefinition("Time");
            foreach (var user in vertices1)
                foreach (var hyper in user.GetAllOutgoingHyperEdges())
                    foreach (var edge in hyper.Item2.GetAllEdges())
                        Console.WriteLine("{0} - {1}   Time: {2}",
                            user.GetPropertyAsString(attrName.ID),
                            edge.GetTargetVertex().GetPropertyAsString(attrName.ID),
                            edge.GetPropertyAsString(attrFriendsDate.ID));
        }

        private void Run()
        {
            /*var UserVertexPredifinition = new VertexTypePredefinition("User")
                     .AddProperty(new PropertyPredefinition("Name", "String"))
                     .AddOutgoingEdge(new OutgoingEdgePredefinition("friends", "User").SetMultiplicityAsMultiEdge());*/

            EdgeTypePredefinition EdgeDate_TypePredefinition = new EdgeTypePredefinition("EdgeDate").
                AddProperty(new PropertyPredefinition("Time", "Int32"));
            var UserVertexPredifinition = new VertexTypePredefinition("User")
                     .AddProperty(new PropertyPredefinition("Name", "String"))
                     .AddOutgoingEdge(new OutgoingEdgePredefinition("friends", "User").SetMultiplicityAsMultiEdge("EdgeDate"));


            var UserEdge = GraphDSServer.CreateEdgeType<IEdgeType>(
               SecToken,
               TransactionID,
               new RequestCreateEdgeType(EdgeDate_TypePredefinition),
               (Statistics, EdgeType) => EdgeType);

            var UserVertex = GraphDSServer.CreateVertexType<IVertexType>(
               SecToken,
               TransactionID,
               new RequestCreateVertexType(UserVertexPredifinition),
               (Statistics, VertexTypes) => VertexTypes);

            DateTime dt = DateTime.Now;
            DateTime full_time = DateTime.Now;

            ReadFile();

            Console.WriteLine("Time of reading: {0}", DateTime.Now - dt);
            dt = DateTime.Now;

            for (int i = 0; i < vertices; ++i)
            {
                EdgePredefinition temp_edge = new EdgePredefinition("friends");
                for (int j = 0; j < edge_list[i].Count; ++j)
                    temp_edge.AddEdge(new EdgePredefinition()
                                        .AddStructuredProperty("Time", edge_list[i][j].Value)
                                        .AddVertexID("User", edge_list[i][j].Key));

                var ins = GraphDSServer.Insert<IVertex>(
                         SecToken,
                         TransactionID,
                         new RequestInsertVertex("User")
                             .AddStructuredProperty("Name", "User" + i.ToString())
                             .AddUnknownProperty("VertexID", i)
                             .AddEdge(temp_edge),
                         (Statistics, Result) => Result);
            }

            Console.WriteLine("Time of inserting: {0}", DateTime.Now - dt);
            dt = DateTime.Now;

            edge_list.Clear();
            
            //PrintAllVerticesInBase(UserVertex);

            //Console.WriteLine("Time of selecting: {0}", DateTime.Now - dt);
            //Console.WriteLine("Full time: {0}", DateTime.Now - full_time);

            //var qres_dij = GraphDSServer.Query(SecToken, TransactionID,
            //                    "FROM User U SELECT U.Name, U.friends.Dijkstra(U.Name = 'User4', 5, 'EdgeDate') AS 'path' WHERE U.Name = 'User0'",
            //                    SonesGQLConstants.GQL);

            //var qres = GraphDSServer.Query(SecToken, TransactionID,
            //                                "FROM User U SELECT U.Name, U.friends.PATH(U.Name = 'User4', 5, 5, true, false) AS 'path' WHERE U.Name = 'User0' OR U.Name = 'User1'",
            //                                SonesGQLConstants.GQL);

            //Console.WriteLine("Time of BFS: {0}", DateTime.Now - dt);
            //dt = DateTime.Now;

            //CheckResult(qres);

            //var first_vert = qres.Vertices.FirstOrDefault();

            //foreach (var vert in qres.Vertices)
            //    PrintWays(vert);

            //Console.WriteLine("Time of printing: {0}", DateTime.Now - dt);
            //Console.WriteLine("Full time: {0}", DateTime.Now - full_time);
        }

        private void PrintNeighbours(IQueryResult qres)
        {
            DateTime dt = DateTime.Now;

            StreamWriter sw = new StreamWriter("netData_output.txt");
            foreach (var vert in qres.Vertices)
            {
                sw.Write("Neighbours of {0}: ", vert.GetPropertyAsString("Name"));
                var neighbours_list = vert.GetProperty<List<VertexView>>("friends");
                if (neighbours_list != null)
                {
                    sw.WriteLine("Count: {0}", neighbours_list.Count);
                    foreach (var neighbour in neighbours_list)
                        sw.Write("{0} ", neighbour.GetPropertyAsString("VertexID"));
                }
                else
                    sw.Write("None");
                sw.WriteLine();
            }
            sw.Close();

            Console.WriteLine("Time of printing to file: {0}", DateTime.Now - dt);
        }

        private void Neighbours()
        {
            DateTime dt = DateTime.Now;
            //var qres = GraphDSServer.Query(SecToken, TransactionID,
            //                                "FROM User SELECT Name, friends.neighbours(2)",
            //                                SonesGQLConstants.GQL);

            //Console.WriteLine("Time of finding neighbours: {0}", DateTime.Now - dt);
            //PrintNeighbours(qres);

            //dt = DateTime.Now;

            //var qres = GraphDSServer.Query(SecToken, TransactionID,
            //                    "FROM User SELECT Name, friends.neighbours(5)",
            //                    SonesGQLConstants.GQL);

            //Console.WriteLine("Time of finding neighbours: {0}", DateTime.Now - dt);
            //PrintNeighbours(qres);

            //dt = DateTime.Now;

            var qres = GraphDSServer.Query(SecToken, TransactionID,
                    "FROM User SELECT Name, friends.neighbours(5)",
                    SonesGQLConstants.GQL);

            CheckResult(qres);

            Console.WriteLine("Time of finding neighbours: {0}", DateTime.Now - dt);
            PrintNeighbours(qres);

            //dt = DateTime.Now;
        }

        private void FastImport()
        {
            //ClearGraphDB();
            //var cr = GraphDSServer.Query(SecToken, TransactionID,
            //                    "CREATE VERTEX TYPE User ATTRIBUTES (String Name, Int64 Age, Set<User> Friends)",
            //                    SonesGQLConstants.GQL);

            var imp = GraphDSServer.Query(SecToken, TransactionID,
                                "IMPORT FROM 'file:\\\\10k_import.xml' FORMAT FastImport",
                                SonesGQLConstants.GQL);
            
        }

        static void Main(string[] args)
        {
            GraphDBTest DB = new GraphDBTest();
            Console.WriteLine("DB was started successfull!");
            DB.Run();
            //DB.FastImport()

            DB.Neighbours();
        }
    }
}
