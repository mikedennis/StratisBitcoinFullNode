﻿using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// RPC Integration tests that require their own node(s) for each test because they change node state.
    /// </summary>
    public class RPCTestsMutable
    {
        [Fact]
        public void TestRpcGetBalanceIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node = builder.CreateStratisPowNode(KnownNetworks.RegTest).NotInIBD();                
                builder.StartAll();
                node.WithWallet();
                RPCClient rpcClient = node.CreateRPCClient();

                int maturity = (int)KnownNetworks.RegTest.Consensus.CoinbaseMaturity;

                // This shouldn't be necessary but is required to make this test pass.
                // - looks like a bug in the wallet?
                //maturity--;
                rpcClient.Generate(maturity);  
                Assert.Equal(Money.Zero, rpcClient.GetBalance());

                rpcClient.Generate(1);
                Assert.Equal(Money.Coins(50), rpcClient.GetBalance());
            }
        }

        [Fact]
        public void TestRpcGetBlockWithValidHashIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node = builder.CreateStratisPowNode(KnownNetworks.RegTest).NotInIBD();
                builder.StartAll();
                RPCClient rpcClient = node.CreateRPCClient();
                node.WithWallet();
                TestHelper.MineBlocks(node, 2);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == 2);

                uint256 blockId = rpcClient.GetBestBlockHash();
                Block block = rpcClient.GetBlock(blockId);
                Assert.True(block.CheckMerkleRoot());
            }
        }

        [Fact]
        public void TestRpcSendToAddressIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = KnownNetworks.RegTest;
                var node = builder.CreateStratisPowNode(network);
                builder.StartAll();
                RPCClient rpcClient = node.CreateRPCClient();
                node.NotInIBD().WithWallet();
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                rpcClient.WalletPassphrase("password", 60);
                var txid = rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m));
                rpcClient.SendCommand(RPCOperations.walletlock);

                // Check the hash calculated correctly.
                var tx = rpcClient.GetRawTransaction(txid);
                Assert.Equal(txid, tx.GetHash());

                // Check the output is the right amount.
                var coin = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == aliceAddress.ScriptPubKey);
                Assert.Equal(coin.Amount, Money.Coins(1.0m));
            }        
        }

        [Fact]
        public void TestRpcSendToAddressCantSpendWhenLocked()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = KnownNetworks.RegTest;
                var node = builder.CreateStratisPowNode(network);
                builder.StartAll();
                RPCClient rpcClient = node.CreateRPCClient();
                node.NotInIBD().WithWallet();
                int blocksToMine = (int)network.Consensus.CoinbaseMaturity + 1;
                TestHelper.MineBlocks(node, blocksToMine);
                TestHelper.WaitLoop(() => node.FullNode.GetBlockStoreTip().Height == blocksToMine);

                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();

                // Not unlockedcase.                
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));

                // Unlock and lock case.
                rpcClient.WalletPassphrase("password", 60);
                rpcClient.SendCommand(RPCOperations.walletlock);
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));

                // Unlock timesout case.
                rpcClient.WalletPassphrase("password", 5);
                Thread.Sleep(120 * 1000); // 2 minutes.
                Assert.Throws<RPCException>(() => rpcClient.SendToAddress(aliceAddress, Money.Coins(1.0m)));
            }
        }

        [Fact]
        public void TestRpcGetTransactionIsSuccessful()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                Network network = KnownNetworks.RegTest;
                var node = builder.CreateStratisPowNode(network);
                builder.StartAll();
                RPCClient rpc = node.CreateRPCClient();
                node.NotInIBD().WithWallet();       
                var blockHash = rpc.Generate(1)[0];
                var block = rpc.GetBlock(blockHash);
                var walletTx = rpc.SendCommand(RPCOperations.gettransaction, block.Transactions[0].GetHash().ToString());
                walletTx.ThrowIfError();
            }
        }
    }
}
