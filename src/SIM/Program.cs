﻿namespace SIM
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;          
  using CommandLine;
  using JetBrains.Annotations;
  using Newtonsoft.Json;
  using SIM.Common;
  using SIM.IO;
  using SIM.Serialization;
  using SIM.Verbs;

  public static class Program
  {
    public static void Main([NotNull] string[] args)
    {
      CoreApp.InitializeLogging();

      CoreApp.LogMainInfo();

      Analytics.Start();

      var filteredArgs = args.ToList();
      var query = GetQueryAndFilterArgs(filteredArgs);
      var wait = GetWaitAndFilterArgs(filteredArgs);

      var parser = new Parser(with =>
      {
        with.MutuallyExclusive = true;
        with.HelpWriter = Console.Error;
      });
        
      var options = new VerbsPalette();
      EnsureAutoCompeteForCommands(options);
      ICommand verb = null;
      if (!parser.ParseArguments(filteredArgs.ToArray(), options, (str, v) => verb = (ICommand)v))
      {
        Console.WriteLine("Note, commands provide output when work is done i.e. without any progress indication.");
        Console.WriteLine("\r\n  --query\t   When specified, allows returning only part of any command's output");
        Console.WriteLine("\r\n  --data\t   When specified, allows returning only 'data' part of any command's output");
        Console.WriteLine("\r\n  --wait\t   When specified, waits for keyboard input before terminating");

        Environment.Exit(Parser.DefaultExitCodeFail);
      }

      Assert.IsNotNull(verb, nameof(verb));

      var commandResult = verb.Execute();
      Assert.IsNotNull(commandResult, nameof(commandResult));

      var result = QueryResult(commandResult, query);
      if (result == null)
      {
        return;
      }

      var serializer = new Serializer(LocalFileSystem.Default);                                                      

      var writer = Console.Out;
      serializer.Serialize(writer, result);

      if (wait)
      {
        Console.ReadKey();
      }
    }

    private static void EnsureAutoCompeteForCommands(VerbsPalette options)
    {
      foreach (var propertyInfo in options.GetType().GetProperties())
      {
        if (typeof(ICommand).IsAssignableFrom(propertyInfo.PropertyType))
        {
          var verb = propertyInfo.GetCustomAttributes(false).OfType<VerbOptionAttribute>().FirstOrDefault();
          if (verb == null)
          {
            continue;
          }

          var command = verb.LongName;
          if (File.Exists(command))
          {
            continue;
          }

          File.Create(command).Close();
        }
      }
    }

    private static object QueryResult([NotNull] CommandResult result, string query)
    {
      Assert.ArgumentNotNull(result, nameof(result));

      if (string.IsNullOrEmpty(query) || !result.Success)
      {
        return result;
      }

      object obj = result;
      foreach (var chunk in query.Split("./".ToCharArray()))
      {
        if (string.IsNullOrEmpty(chunk))
        {
          continue;
        }

        var newObj = null as object;
        var dictionary = obj as IDictionary;
        if (dictionary != null)
        {
          if (dictionary.Contains(chunk))
          {
            newObj = dictionary[chunk];
          }
        }
        else
        {
          var type = obj.GetType();
          var prop = type.GetProperties().FirstOrDefault(x => x.Name.Equals(chunk, StringComparison.OrdinalIgnoreCase));
          if (prop != null)
          {
            newObj = prop.GetValue(obj, null);
          }
        }

        if (newObj == null)
        {
          Console.WriteLine("Cannot find '" + chunk + "' chunk of '" + query + "' query in the object: ");
          Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));

          return null;
        }

        obj = newObj;
      }

      return obj;
    }

    private static string GetQueryAndFilterArgs([NotNull] List<string> filteredArgs)
    {
      Assert.ArgumentNotNull(filteredArgs, nameof(filteredArgs));

      var query = string.Empty;
      for (var i = 0; i < filteredArgs.Count; i++)
      {
        if (filteredArgs[i] == "--data")
        {
          filteredArgs[i] = "--query";
          filteredArgs.Insert(i + 1, "data");
        }

        if (filteredArgs[i] != "--query")
        {
          continue;
        }

        filteredArgs.RemoveAt(i);

        if (filteredArgs.Count > i)
        {
          query = filteredArgs[i];
          filteredArgs.RemoveAt(i);
        }

        break;
      }

      return query;
    }

    private static bool GetWaitAndFilterArgs([NotNull] List<string> filteredArgs)
    {
      Assert.ArgumentNotNull(filteredArgs, nameof(filteredArgs));

      for (var i = 0; i < filteredArgs.Count; i++)
      {
        if (filteredArgs[i] != "--wait")
        {
          continue;
        }

        filteredArgs.RemoveAt(i);

        return true;
      }

      return false;
    }
  }
}