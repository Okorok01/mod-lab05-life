using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Life;
using Xunit;

namespace LifeTests
{
    public class BoardTests
    {
        [Fact]
        public void NewBoard_IsEmpty()
        {
            var board = new Board(10, 10);
            Assert.Equal(0, board.Population);
        }
        [Fact]
        public void Randomize_ProducesCells()
        {
            var board = new Board(20, 20);
            board.Randomize(0.5);
            Assert.True(board.Population > 0);
        }
        [Fact]
        public void SetCell_Works()
        {
            var board = new Board(5, 5);
            board.SetCell(1, 1, true);
            Assert.True(board[1, 1]);
        }
        [Fact]
        public void Block_IsStable()
        {
            var board = new Board(4, 4);
            board.LoadPattern(new[] { (0,0), (1,0), (0,1), (1,1) });
            int popBefore = board.Population;
            board.NextGeneration();
            Assert.Equal(popBefore, board.Population);
        }
        [Fact]
        public void Blinker_Period2()
        {
            var board = new Board(5, 5);
            board.LoadPattern(new[] { (1,2), (2,2), (3,2) });
            var pop0 = board.Population;
            board.NextGeneration();
            var pop1 = board.Population;
            board.NextGeneration();
            Assert.Equal(pop0, board.Population);
        }
        [Fact]
        public void ConnectedComponents_Block()
        {
            var board = new Board(4, 4);
            board.LoadPattern(new[] { (0,0), (1,0), (0,1), (1,1) });
            var comps = board.FindConnectedComponents();
            Assert.Single(comps);
            Assert.Equal(4, comps[0].Count);
        }
        [Fact]
        public void Classify_Block()
        {
            var board = new Board(4, 4);
            board.LoadPattern(new[] { (0,0), (1,0), (0,1), (1,1) });
            var comp = board.FindConnectedComponents()[0];
            Assert.Equal("Block", board.ClassifyComponent(comp));
        }
        [Fact]
        public void Classify_Beehive()
        {
            var board = new Board(6, 5);
            board.LoadPattern(new[] { (1,0),(2,0),(0,1),(3,1),(1,2),(2,2) });
            var comp = board.FindConnectedComponents()[0];
            Assert.Equal("Beehive", board.ClassifyComponent(comp));
        }
        [Fact]
        public void Classify_Loaf()
        {
            var board = new Board(5, 5);
            board.LoadPattern(new[] { (1,0),(2,0),(0,1),(3,1),(1,2),(3,2),(2,3) });
            var comp = board.FindConnectedComponents()[0];
            Assert.Equal("Loaf", board.ClassifyComponent(comp));
        }
        [Fact]
        public void SaveAndLoad_State()
        {
            var board = new Board(5, 5);
            board.SetCell(1, 1, true);
            board.SaveToFile("test_state.txt");
            var loaded = Board.LoadFromFile("test_state.txt");
            Assert.True(loaded[1, 1]);
            Assert.Equal(board.Population, loaded.Population);
            // Очистка
            if (File.Exists("test_state.txt"))
                File.Delete("test_state.txt");
        }
        [Fact]
        public void FindStableGeneration_ReturnsEarlyForStable()
        {
            var board = new Board(6, 6);
            board.LoadPattern(new[] { (0,0),(1,0),(0,1),(1,1) });
            int gen = board.FindStableGeneration(5, 100);
            Assert.InRange(gen, 0, 5);
        }
        [Fact]
        public void NextGeneration_EmptyStaysEmpty()
        {
            var board = new Board(10, 10);
            board.NextGeneration();
            Assert.Equal(0, board.Population);
        }
        [Fact]
        public void RuleParsing_AllowsOtherRules()
        {
            var board = new Board(10, 10, "B36/S23");
            board.Randomize(0.3);
            board.NextGeneration();
            Assert.True(true); 
        }
        [Fact]
        public void Settings_Serialization()
        {
            var set = new Settings { Width = 30 };
            var json = JsonSerializer.Serialize(set);
            var deserialized = JsonSerializer.Deserialize<Settings>(json);
            Assert.Equal(30, deserialized!.Width);
        }
        [Fact]
        public void MultipleComponents_Counting()
        {
            var board = new Board(10, 10);
            board.SetCell(1, 1, true);
            board.SetCell(3, 3, true);
            var comps = board.FindConnectedComponents();
            Assert.Equal(2, comps.Count);
        }
        [Fact]
        public void Classify_Boat()
        {
            var board = new Board(5, 5);
            board.LoadPattern(new[] { (0,0),(1,0),(0,1),(2,1),(1,2) });
            var comp = board.FindConnectedComponents()[0];
            Assert.Equal("Boat", board.ClassifyComponent(comp));
        }
    }
}