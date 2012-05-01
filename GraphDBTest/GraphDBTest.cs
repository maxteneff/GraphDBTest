using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using sones.GraphDB;
using sones.GraphDB.Request;
using sones.GraphDB.TypeSystem;
using sones.GraphDS.PluginManager;
using sones.GraphDSServer;
using sones.GraphQL;
using sones.GraphQL.Result;
using sones.Library.Commons.Security;
using sones.Library.PropertyHyperGraph;
using sones.Library.VersionedPluginManager;

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
        /// <param name="rest">Определяет, создавать ли rest service или нет</param> 
        public GraphDBTest(bool rest)
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

            if (rest)
            {
                Dictionary<string, object> RestParameter = new Dictionary<string, object>();
                RestParameter.Add("IPAddress", IPAddress.Any);
                RestParameter.Add("Port", 9975);
                RestParameter.Add("Username", "test");
                RestParameter.Add("Password", "test");
                GraphDSServer.StartService("sones.RESTService", RestParameter);
            }
        }
        
        /// <summary>
        /// Создает объект базы данных с выключенным rest
        /// </summary>
        public GraphDBTest() : this(false){ }

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
        /// <param name="path">Путь к файлу</param>
        /// <param name="backward">Делать ли обратные ребра</param>
        private void ReadFile(string path, bool backward = false)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("File does not exist!");
                return;
            }
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

                    edge_list[a].Add( new KeyValuePair<long, Int32>(b, c) );
                    if (backward)
                        edge_list[b].Add(new KeyValuePair<long, Int32>(a, c));
                }
            }
        }

        /// <summary>
        /// Анализирует результат запроса в базу данных. Показывает ошибки, которые были во время запроса.
        /// </summary>
        /// <param name="myQueryResult">Результат запроса</param>
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

            ReadFile("netData.txt");

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
        }

        private void PrintNeighbours(IQueryResult qres)
        {
            DateTime dt = DateTime.Now;
            string path = "netData_output.txt";
            if (!File.Exists(path))
                File.Create(path);
            StreamWriter sw = new StreamWriter(path);
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

            var qres = GraphDSServer.Query(SecToken, TransactionID,
                    "FROM User SELECT Name, friends.neighbours(5)",
                    SonesGQLConstants.GQL);

            CheckResult(qres);

            Console.WriteLine("Time of finding neighbours: {0}", DateTime.Now - dt);
            PrintNeighbours(qres);

            //dt = DateTime.Now;
        }

        private void UnionOfPairIntersections(int depth)
        {
            DateTime dt = DateTime.Now;
            var qres = GraphDSServer.Query(SecToken, TransactionID,
                    "FROM User SELECT Name, friends.neighbours(" + depth.ToString() + ")",
                    SonesGQLConstants.GQL);
            CheckResult(qres);

            var vertices = qres.Vertices;

            string path = "UnionofPairs_output.txt";
            StreamWriter sw = new StreamWriter(path);
            HashSet<int> answer = new HashSet<int>();
            foreach (var vertex1 in vertices)
            {
                foreach (var vertex2 in vertices)
                {

                    Dictionary<int, int> intersection = new Dictionary<int, int>();
                    
                    var neighbours_list = vertex1.GetProperty<List<VertexView>>("friends");
                    if (neighbours_list != null)
                    {
                        foreach (var neighbour in neighbours_list)
                        {
                            int vert_id = neighbour.GetProperty<int>("VertexID");
                            if (intersection.ContainsKey(vert_id))
                                intersection[vert_id]++;
                            else
                                intersection.Add(vert_id, 1);
                        }
                    }
                    neighbours_list = vertex2.GetProperty<List<VertexView>>("friends");
                    if (neighbours_list != null)
                    {
                        foreach (var neighbour in neighbours_list)
                        {
                            int vert_id = neighbour.GetProperty<int>("VertexID");
                            if (intersection.ContainsKey(vert_id))
                                intersection[vert_id]++;
                            else
                                intersection.Add(vert_id, 1);
                        }
                    }

                    
                    if (!File.Exists(path))
                        File.Create(path);
                    
                    Console.WriteLine("Intersection of " + vertex1.GetPropertyAsString("Name") + " " + vertex2.GetPropertyAsString("Name"));
                    
                    foreach (var pair in intersection)
                    {
                        if (pair.Value >= 2)
                            if (!answer.Contains(pair.Key))
                                answer.Add(pair.Key);
                    }
                    
                }

            }

            sw.WriteLine("Count: " + answer.Count);
            foreach (int id in answer)
                sw.Write(id.ToString() + " ");
            sw.Close();
            Console.WriteLine("Time of finding and printing intersection: {0}", DateTime.Now - dt);
        }

        static void Main(string[] args)
        {
            GraphDBTest DB = new GraphDBTest();
            Console.WriteLine("DB was started successfull!");
            DB.Run();

            //DB.Neighbours();
            DB.UnionOfPairIntersections(2);
        }

        /// <summary>
        /// Trash //////////////////////////////////////////////////////////////////////////
        /// </summary>

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
    }
}
