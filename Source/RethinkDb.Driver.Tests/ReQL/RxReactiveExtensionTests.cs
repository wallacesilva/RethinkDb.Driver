﻿using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RethinkDb.Driver.Model;
using RethinkDb.Driver.Tests.Utils;

namespace RethinkDb.Driver.Tests.ReQL
{
    [TestFixture]
    public class RxReactiveExtensionTests : QueryTestFixture
    {
        [Test]
        public void basic_change_feed_with_reactive_extensions()
        {
            var onCompleted = 0;
            var onError = 0;
            var onNext = 0;

            var result = r.db(DbName).table(TableName)
                    .delete()[new { return_changes = true }]
                    .runResult(conn)
                    .AssertNoErrors();

            result.ChangesAs<JObject>().Dump();

            var changes = r.db(DbName).table(TableName)
                //.changes()[new {include_states = true, include_initial = true}]
                .changes()
                .runChanges<JObject>(conn);

            var observable = changes.ToObservable();

            //use a new thread if you want to continue,
            //otherwise, subscription will block.
            observable.SubscribeOn(NewThreadScheduler.Default)
                .Subscribe(
                    x => OnNext(x, ref onNext),
                    e => OnError(e, ref onError),
                    () => OnCompleted(ref onCompleted)
                );

            Thread.Sleep(3000);

            Task.Run(() =>
                {
                    r.db(DbName).table(TableName)
                        .insert(new { foo = "bar" })
                        .run(conn);
                });

            Thread.Sleep(3000);

            Task.Run(() =>
            {
                r.db(DbName).table(TableName)
                    .insert(new { foo = "bar" })
                    .run(conn);
            });

            Thread.Sleep(3000);

            Task.Run(() =>
                {
                    r.db(DbName).table(TableName)
                        .insert(new { foo = "bar" })
                        .run(conn);
                });

            Thread.Sleep(3000);

            changes.close();

            onCompleted.Should().Be(1);
            onNext.Should().Be(3);
            onError.Should().Be(0);
        }

        private void OnCompleted(ref int onCompleted)
        {
            Console.WriteLine("On Completed.");
            onCompleted++;
        }

        private void OnError(Exception obj, ref int onError)
        {
            Console.WriteLine("On Error");
            Console.WriteLine(obj.Message);
            onError++;
        }

        private void OnNext(Change<JObject> obj, ref int onNext)
        {
            Console.WriteLine("On Next");
            obj.Dump();
            onNext++;
        }

        [Test]
        [Explicit]
        public void change_feeds_without_rx()
        {
            var result = r.db(DbName).table(TableName)
                .delete()[new {return_changes = true}]
                .runResult(conn)
                .AssertNoErrors();

            var changes = r.db(DbName).table(TableName)
                .changes()[new {include_states = true}]
                .runChanges<JObject>(conn);

            changes.close();
        }
    }
}