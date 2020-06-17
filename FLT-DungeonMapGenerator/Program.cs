using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

/*
 * FLT, 2020
 * Just playing with code, that's all.
 * Last modified 2020.06.17
 * 
 * skycraper3@gmail.com
 */

namespace FLT_DungeonMapGenerator
{
    class Program
    {
        private class MapMeta
        {
            // Map
            public int MapSizeX { get; set; }
            public int MapSizeY { get; set; }

            // Room
            public int StartRoomSize { get; set; }
            public int MaxRoomNumber { get; set; }
            public int MinRoomSize { get; set; }    // Minimum 2
            public int MaxRoomSize { get; set; }
            public int MaxRoomConnection { get; set; }
            public int MinRoomDistance { get; set; }

            // Corridor
            public int MinCorridorStraight { get; set; }
            public int MaxCorridorStraight { get; set; }
            public int RouteTwistFactor { get; set; }   // Higher means low chance, 3 = 33%, 4 = 25% ...
            public int RouteDirectFactor { get; set; }   // Higher means low chance, 3 = 33%, 4 = 25% ...
            public int RouteBranchFactor { get; set; }
            public int MaxCorridorBranchCount { get; set; }
            public int RouteAdditionalBranchFactor { get; set; } // recommend 5 < factor
        }

        // #0 is always Starting room
        private class RoomMeta
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Connections { get; set; }
        }

        private class Corridors
        {
            public Stack<Point> CorridorData { get; set; }
            public bool IsBranch { get; set; }
            public int CorridorStartAt { get; set; }
            public int CorridorEndAt { get; set; }
        }

        private enum TileType
        {
            WALL = 0,
            FLOOR,
            FLOOR_START,
            TEST,
        };

        private class Prefabs
        {
            public string Name { get; set; }
            public int Tile { get; set; }
            public int SpawnChance { get; set; } // chance / 1000
        }
        
        private const int E = 0;
        private const int W = 1;
        private const int S = 2;
        private const int N = 3;
        private const int ChancePool = 1000;
        private const string filePath = "TestTemplate.txt";

        static void Main(string[] args)
        {
            Console.WriteLine("FLT-DungeonMapGenerator");

            MapMeta meta = new MapMeta
            {
                MapSizeX = 96,
                MapSizeY = 64,
                StartRoomSize = 6,
                MaxRoomSize = 8,
                MinRoomSize = 4,
                MaxRoomNumber = 10,
                MaxRoomConnection = 2,
                MinRoomDistance = 200,
                MaxCorridorBranchCount = 100,
                MinCorridorStraight = 3,
                MaxCorridorStraight = 12,
                RouteTwistFactor = 3,
                RouteDirectFactor = 6,
                RouteBranchFactor = 12,
                RouteAdditionalBranchFactor = 12,
            };

            char[][] map;
            List<RoomMeta> rooms = new List<RoomMeta>();
            List<Corridors> corridors = new List<Corridors>();
            List<Prefabs> prefabs = new List<Prefabs>
            {
                new Prefabs { Name = "Test", Tile = (int)TileType.TEST, SpawnChance = 20, } // 2%
            };
            List<string> componentsPath = new List<string>
            {
                filePath
            };

            Console.WriteLine("Initializing...");
            map = Stage1_MapInit(meta);

            // Maybe can merge Stage2 and Stage3
            Console.WriteLine("Making starting area...");
            Stage2_MakeStartRoom(ref map, meta, rooms);

            Console.WriteLine("Making rooms...");
            Stage3_MakeRooms(ref map, meta, rooms);

            Console.WriteLine("Making corridors...");
            Stage4_ConnectRooms(ref map, meta, rooms, corridors, 0, 0);

            Console.WriteLine("Current corridors=" + corridors.Count);

            Console.WriteLine("Making branches...");
            Stage5_MakeCorridorBranch(ref map, meta, rooms, corridors);
            
            Console.WriteLine("Current corridors=" + corridors.Count);

            Console.WriteLine("Validating connections...");
            Stage6_ConnectionValidate(ref map, meta, rooms, corridors);
            
            Console.WriteLine("Current corridors=" + corridors.Count);

            Console.WriteLine("Making entities...");
            Stage7_MakePrefab(ref map, meta, corridors, prefabs);

            Console.WriteLine("Current corridors=" + corridors.Count);
            Stage8_InsertComponent(ref map, meta, rooms, componentsPath);

            // Draw on Console
            for (int j = 0; j < meta.MapSizeY; j++)
            {
                for (int i = 0; i < meta.MapSizeX; i++)
                {
                    if (map[i][j] == (char)TileType.WALL)
                        Console.Write('X');
                    else if (map[i][j] == (char)TileType.FLOOR)
                        Console.Write(' ');
                    else if (map[i][j] == (char)TileType.FLOOR_START)
                        Console.Write('@');
                    else if (map[i][j] == (char)TileType.TEST)
                        Console.Write('T');
                    else
                        Console.Write('?');
                }

                Console.Write(Environment.NewLine);
            }

            Console.ReadKey();
        }

        #region Stage1_MapInit
        private static char[][] Stage1_MapInit(MapMeta meta)
        {
            char[][] map = new char[meta.MapSizeX][];

            for (int i = 0; i < meta.MapSizeX; i++)
            {
                map[i] = new char[meta.MapSizeY];
            }

            return map;
        }
        #endregion

        #region Stage2_MakeStartRoom
        private static void Stage2_MakeStartRoom(ref char[][] map, MapMeta meta, List<RoomMeta> rooms)
        {
            Random r = new Random((int)DateTime.Now.Ticks);

            int x = r.Next(0, meta.MapSizeX);
            int y = r.Next(0, meta.MapSizeY);

            for (int i = 0; i < meta.StartRoomSize; i++)
            {
                for (int j = 0; j < meta.StartRoomSize; j++)
                {
                    int pan = meta.StartRoomSize / 2;

                    if (x + (i - pan) >= 0 && x + (i - pan) < meta.MapSizeX &&
                        y + (j - pan) >= 0 && y + (j - pan) < meta.MapSizeY)
                    {
                        map[x + (i - pan)][y + (j - pan)] = (char)TileType.FLOOR;

                        if ((i - pan == 0) && (j - pan == 0))
                        {
                            map[x + (i - pan)][y + (j - pan)] = (char)TileType.FLOOR_START;

                            rooms.Add(new RoomMeta {
                                X = x + (i - pan),
                                Y = y + (j - pan),
                                Width = meta.StartRoomSize,
                                Height = meta.StartRoomSize,
                                Connections = 0
                            });
                        }
                    }
                }
            }
        }
        #endregion

        #region Stage3_MakeRooms
        private static void Stage3_MakeRooms(ref char[][] map, MapMeta meta, List<RoomMeta> rooms)
        {
            // TODO: Try to remove overlapping
            for (int k = 0; k < meta.MaxRoomNumber; )
            {
                RetryRoomPositioning:
                int x = RandomNumber.Between(0, meta.MapSizeX);
                int y = RandomNumber.Between(0, meta.MapSizeY);

                Point pt = new Point(x, y);

                for (int i = 0; i < rooms.Count; i++)
                {
                    if ((pt - new Point(rooms[i].X, rooms[i].Y)).LengthSquared < meta.MinRoomDistance)
                    {
                        //Console.WriteLine("Failed to make room, dist=" + (pt - new Point(rooms[i].X, rooms[i].Y)).LengthSquared
                        //    + ", but meta.MinRoomDistance is " + meta.MinRoomDistance);
                        goto RetryRoomPositioning;
                    }
                }
                
                int w = RandomNumber.Between(meta.MinRoomSize, meta.MaxRoomSize);
                int h = RandomNumber.Between(meta.MinRoomSize, meta.MaxRoomSize);

                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        int panW = w / 2;
                        int panH = h / 2;

                        if (x + (i - panW) >= 0 && x + (i - panW) < meta.MapSizeX &&
                            y + (j - panH) >= 0 && y + (j - panH) < meta.MapSizeY)
                        {
                            if (map[x + (i - panW)][y + (j - panH)] != (char)TileType.FLOOR_START)
                            {
                                map[x + (i - panW)][y + (j - panH)] = (char)TileType.FLOOR;
                            }

                            if ((i - panW == 0) && (j - panH == 0))
                            {
                                rooms.Add(new RoomMeta
                                {
                                    X = x + (i - panW),
                                    Y = y + (j - panH),
                                    Width = w,
                                    Height = h,
                                    Connections = 0
                                });
                            }
                        }
                    }
                }

                k++;
            }
        }
        #endregion

        #region Stage4_ConnectRooms
        private static void Stage4_ConnectRooms(
            ref char[][] map, 
            MapMeta meta, 
            List<RoomMeta> rooms, 
            List<Corridors> corridors,
            int overrideRoomStart,
            int overrideRoomTarget)
        {
            bool completeFlag = false;
            bool overrideFlag = false;

            if (overrideRoomStart != 0 || overrideRoomTarget != 0)
                overrideFlag = true;

            if (meta.MaxRoomNumber != rooms.Count)
            {
                meta.MaxRoomNumber = rooms.Count;
            }

            while (completeFlag == false)
            {
                Stack<Point> stack = new Stack<Point>();
                int targetRoom = RandomNumber.Between(1, meta.MaxRoomNumber - 1);
                int startRoom = 0;
                int maxConn = RandomNumber.Between(1, meta.MaxRoomConnection);
                
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].Connections < maxConn)
                    {
                        startRoom = i;
                        break;
                    }

                    // there is no more room that 0 connection
                    if (i == rooms.Count - 1)
                    {
                        completeFlag = true;
                    }
                }

                if (overrideFlag)
                {
                    startRoom = overrideRoomStart;
                    targetRoom = overrideRoomTarget;
                    completeFlag = true;
                }

                // starting position
                stack.Push(new Point { X = rooms[startRoom].X, Y = rooms[startRoom].Y });

                Point start = stack.Pop();
                int curX = (int)start.X;
                int curY = (int)start.Y;
                double dist = double.MaxValue;
                int cs = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);
                int headTo = 0;

                while (dist > 1.412)
                {
                    // Readjust point that out of bound
                    double distN = (new Point(curX, curY + 1) - new Point(rooms[targetRoom].X, rooms[targetRoom].Y)).LengthSquared;
                    double distS = (new Point(curX, curY - 1) - new Point(rooms[targetRoom].X, rooms[targetRoom].Y)).LengthSquared;
                    double distW = (new Point(curX - 1, curY) - new Point(rooms[targetRoom].X, rooms[targetRoom].Y)).LengthSquared;
                    double distE = (new Point(curX + 1, curY) - new Point(rooms[targetRoom].X, rooms[targetRoom].Y)).LengthSquared;

                    double min = Math.Min(distE, Math.Min(distW, Math.Min(distN, distS)));

                    // Corridor twister here (set on meta) - 0, 4 = 25%
                    int rb = RandomNumber.Between(0, meta.RouteTwistFactor);
                    
                    if (rb == 0)
                    {
                        int rc = RandomNumber.Between(0, 4);

                        if (rc == N) min = distN;
                        else if (rc == S) min = distS;
                        else if (rc == W) min = distW;
                        else if (rc == E) min = distE;
                    }

                    dist = min;

                    // Boundary check
                    if (curX > meta.MapSizeX) { curX--; continue; }
                    else if (curY > meta.MapSizeY) { curY--; continue; }
                    else if (curX - 1 < 0) { curX++; continue; }
                    else if (curY - 1 < 0) { curY++; continue; }

                    // Push cursor
                    if (min == distN) { stack.Push(new Point { X = curX, Y = ++curY }); headTo = N; }
                    if (min == distS) { stack.Push(new Point { X = curX, Y = --curY }); headTo = S; }
                    if (min == distW) { stack.Push(new Point { X = --curX, Y = curY }); headTo = W; }
                    if (min == distE) { stack.Push(new Point { X = ++curX, Y = curY }); headTo = E; }

                    // Make corridor straight
                    int straight = RandomNumber.Between(0, meta.RouteDirectFactor);

                    if (straight == 0)
                    {
                        cs = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);

                        switch (headTo)
                        {
                            case N:
                                for (int i = 0; i < cs; i++)
                                    stack.Push(new Point { X = curX, Y = ++curY });
                                break;
                            case S:
                                for (int i = 0; i < cs; i++)
                                    stack.Push(new Point { X = curX, Y = --curY });
                                break;
                            case W:
                                for (int i = 0; i < cs; i++)
                                    stack.Push(new Point { X = --curX, Y = curY });
                                break;
                            case E:
                                for (int i = 0; i < cs; i++)
                                    stack.Push(new Point { X = ++curX, Y = curY });
                                break;
                            default:
                                break;
                        }
                    }
                }

                // Save corridors data
                Stack<Point> tmp = new Stack<Point>();
                tmp = CopyStack.Clone(stack);
                Corridors crd = new Corridors
                {
                    CorridorData = tmp,
                    IsBranch = false,
                    CorridorStartAt = startRoom,
                    CorridorEndAt = targetRoom,
                };
                corridors.Add(crd);

                // Draw path
                while (stack.Count > 0)
                {
                    Point pt = stack.Pop();

                    if (pt.X < meta.MapSizeX &&
                        pt.X >= 0 &&
                        pt.Y < meta.MapSizeY &&
                        pt.Y >= 0)
                    {
                        if (map[(int)pt.X][(int)pt.Y] != (char)TileType.FLOOR_START)
                        {
                            map[(int)pt.X][(int)pt.Y] = (char)TileType.FLOOR;
                        }
                    }
                }

                // Add connections
                rooms[startRoom].Connections++;
                rooms[targetRoom].Connections++;
            }
        }
        #endregion

        #region Stage5_MakeCorridorBranch
        private static void Stage5_MakeCorridorBranch(
            ref char[][]    map, 
            MapMeta         meta, 
            List<RoomMeta>  rooms, 
            List<Corridors> corridors)
        {
            // TODO: Beautify random corridors

            // Make branches of exist corridors
            for (int i = 0; i < corridors.Count; i++)
            {
                Stack<Point> stack = corridors[i].CorridorData;

                if (corridors.Count > meta.MaxCorridorBranchCount)
                    break;
                
                for (int j = 0; stack.Count > 0; j++)
                {
                    Point coord = stack.Pop();

                    if (coord.X < 0 || coord.X + 1 > meta.MapSizeX ||
                        coord.Y < 0 || coord.Y + 1 > meta.MapSizeY)
                    {
                        continue;
                    }

                    if (corridors[i].IsBranch == false &&
                        RandomNumber.Between(0, meta.RouteBranchFactor) == 0)
                    {
                        Aux_MakeRandomCorridor(ref map, meta, coord, corridors);
                    }
                    else if(corridors[i].IsBranch == true &&
                            RandomNumber.Between(0, meta.RouteAdditionalBranchFactor) == 0)
                    {
                        Aux_MakeRandomCorridor(ref map, meta, coord, corridors);
                    }
                }
            }
        }
        #endregion

        #region Stage6_ConnectionValidate
        private static void Stage6_ConnectionValidate(
            ref char[][] map,
            MapMeta meta,
            List<RoomMeta> rooms,
            List<Corridors> corridors)
        {
            bool[] connected = new bool[rooms.Count];
            LinkedList<int> list = new LinkedList<int>();

            for (int i = 1; i < connected.Length; i++)
            {
                if (connected[i] == false)
                {
                    Aux_RoomValidationUpdate(ref connected, list, corridors);
                    Stage4_ConnectRooms(ref map, meta, rooms, corridors, 0, i);
                    connected[i] = true;
                }
            }
        }
        #endregion

        #region Stage7_MakePrefab
        private static void Stage7_MakePrefab(
            ref char[][] map, 
            MapMeta meta, 
            List<Corridors> corridors, 
            List<Prefabs> prefabs)
        {
            // If tile is Floor, have chance to make something
            bool[][] visited = new bool[meta.MapSizeX][];

            for (int i = 0; i < meta.MapSizeX; i++)
            {
                visited[i] = new bool[meta.MapSizeY];
            }

            for (int i = 0; i < corridors.Count; i++)
            {
                Stack<Point> stack = corridors[i].CorridorData;

                for (int j = 0; j < stack.Count; j++)
                {
                    Point pt = stack.Pop();

                    // Not sure why this sentence hit
                    if (pt.X + 1 > meta.MapSizeX || pt.X < 0 ||
                        pt.Y + 1 > meta.MapSizeY || pt.Y < 0)
                    {
                        continue;
                    }

                    if (map[(int)pt.X][(int)pt.Y] == (char)TileType.FLOOR &&
                        visited[(int)pt.X][(int)pt.Y] == false)
                    {
                        for (int k = 0; k < prefabs.Count; k++)
                        {
                            if(RandomNumber.Between(0, (int)(ChancePool / (double)prefabs[k].SpawnChance))
                                == 0)
                            {
                                map[(int)pt.X][(int)pt.Y] = (char)prefabs[k].Tile;
                            }

                            visited[(int)pt.X][(int)pt.Y] = true;
                        }
                    }
                }
            }
        }
        #endregion

        #region Stage8_InsertComponent
        private static void Stage8_InsertComponent(ref char[][] map, MapMeta meta, List<RoomMeta> rooms, List<string> componentsPath)
        {
            // Insert pre built map from filePath, but not near rooms[0] (starting area)
            foreach (string filePath in componentsPath)
            {
                string[] dataRaw = File.ReadAllLines(filePath);
                var totalRow = dataRaw.Length;

                if (totalRow < 2) continue;

                var chance = Convert.ToInt32(dataRaw[0]);

                // get component size
                int width = dataRaw[1].Length;
                int height = totalRow - 1;

                // choose random coordinates
                RetryComponentPositioning:
                int x = RandomNumber.Between(0, meta.MapSizeX - width - 1);
                int y = RandomNumber.Between(0, meta.MapSizeY - height - 1);

                Point pt = new Point(x, y);

                if ((pt - new Point(rooms[0].X, rooms[0].Y)).LengthSquared < meta.MinRoomDistance ||
                    (pt - new Point(rooms[0].X - width, rooms[0].Y - height)).LengthSquared < meta.MinRoomDistance)
                {
                    goto RetryComponentPositioning;
                }

                // try roll
                if (RandomNumber.Between(0, (int)(ChancePool / Convert.ToDouble(dataRaw[0]))) == 0)
                {
                    Console.WriteLine("Inserting component : " + filePath + ", size=" + width + "x" + height);

                    // insert
                    for (int i = 0; i < height; i++)
                    {
                        string line = dataRaw[i + 1];

                        for (int j = 0; j < width; j++)
                        {
                            map[x + j][y + i] = (char)(line[j] - '0');
                        }
                    }
                }

                // TODO: try connect if isolated

                return;
            }
        }
        #endregion

        #region Aux_RoomValidationUpdate
        private static void Aux_RoomValidationUpdate(ref bool[] connected, LinkedList<int> list, List<Corridors> corridors)
        {
            for (int j = 0; j < corridors.Count; j++)
            {
                for (int i = j; i < corridors.Count; i++)
                {
                    int start = corridors[i].CorridorStartAt;
                    int end = corridors[i].CorridorEndAt;

                    if (list.Count == 0)
                    {
                        list.AddFirst(start);
                        connected[start] = true;
                        list.AddLast(end);
                        connected[end] = true;
                    }
                    else
                    {
                        if (list.Find(start) != null)
                        {
                            list.AddLast(end);
                            connected[start] = true;
                            connected[end] = true;
                        }
                        if (list.Find(end) != null)
                        {
                            list.AddLast(start);
                            connected[start] = true;
                            connected[end] = true;
                        }
                    }
                }
            }
        }
        #endregion

        #region Aux_MakeRandomCorridor
        private static void Aux_MakeRandomCorridor(ref char[][] map, MapMeta meta, Point coord, List<Corridors> corridors)
        {
            int headTo = RandomNumber.Between(0, 4);
            int direction = RandomNumber.Between(0, 3); // +/- , + , -
            int lengthPositive = 0, lengthNegative = 0;
            Stack<Point> path = new Stack<Point>();
            Point baseCoord = new Point(coord.X, coord.Y);

            if (direction == 0)
            {
                lengthPositive = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);
                lengthNegative = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);
            }
            else if (direction == 1)
                lengthPositive = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);
            else
                lengthNegative = RandomNumber.Between(meta.MinCorridorStraight, meta.MaxCorridorStraight);

            for (int i = 0; i < lengthPositive; i++)
            {
                switch(headTo)
                {
                    case N:
                    case S:
                        coord.Y++; break;
                    case W:
                    case E:
                        coord.X++; break;
                    default:
                        break;
                }

                if (coord.X + 1 > meta.MapSizeX) { coord.X--; continue; }
                else if (coord.Y + 1 > meta.MapSizeY) { coord.Y--; continue; }

                if (map[(int)coord.X][(int)coord.Y] != (char)TileType.FLOOR_START)
                    map[(int)coord.X][(int)coord.Y] = (char)TileType.FLOOR;

                path.Push(coord);
            }

            coord = baseCoord;

            for (int i = 0; i < lengthNegative; i++)
            {
                switch (headTo)
                {
                    case N:
                    case S:
                        coord.Y--; break;
                    case W:
                    case E:
                        coord.X--; break;
                    default:
                        break;
                }

                if (coord.X - 1 < 0) { coord.X++; continue; }
                else if (coord.Y - 1 < 0) { coord.Y++; continue; }

                if (map[(int)coord.X][(int)coord.Y] != (char)TileType.FLOOR_START)
                    map[(int)coord.X][(int)coord.Y] = (char)TileType.FLOOR;

                path.Push(coord);
            }

            Corridors crd = new Corridors
            {
                CorridorData = path,
                IsBranch = true,
                CorridorStartAt = -1,
                CorridorEndAt = -1,
            };

            corridors.Add(crd);
        }
        #endregion
    }
}
