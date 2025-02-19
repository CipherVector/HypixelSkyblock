using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet;
using Coflnet.Sky.Core;

public class Migrator {
    public static void Migrate () {
        try {
            Console.WriteLine ("Migrating to db");
            GenerateDBIndexable ();
        } catch (Exception e) {
            Console.WriteLine ($"failed to migrate {e.Message} \n {e.StackTrace}");
            StorageManager.Save ().Wait ();
        }
    }

    /// <summary>
    /// Converts the file db to the pull-Format and saves it to be picket up by the indexer
    /// </summary>
    private static void GenerateDBIndexable () {
        for (int i = 0; i < 4; i++) {
            var list = GetAThousand ();
            Console.Write ($"Got a total of {list.Count}");
            FileController.SaveAs ($"apull/{list.First().Uuid}", list);
            int validated = 0;
            // delete them
            foreach (var item in list)
            {
                var path = $"importedAuctions/{item.Uuid.Substring(0,2)}/{item.Uuid.Substring(2,3)}";

                try {
                    if(!FileController.Exists(path))
                    {
                        // has likely already been tested
                        continue;
                    }
                    var data = FileController.LoadAs<Dictionary<string, SaveAuction>> (path);
                    if(data.Select(k=>k.Value).Except(list).Any())
                    {
                        // something is missing, pull back
                        Console.WriteLine($"whoops import incomplete {path}");
                    } else {
                        // all is converted
                        validated+=data.Count;
                        FileController.Delete(path);
                    }
                } catch(Exception e)
                {
                    Console.WriteLine($"Error while converting {e.Message} {e.StackTrace}");
                }
            }
            Console.WriteLine($" Validated: {validated}");
        }

    }


    static List<SaveAuction> GetAThousand () {
        var path = "nauctions";
        var completeAuctions = new List<SaveAuction> ();
        foreach (var item in FileController.DirectoriesNames ("*", path)) {
            var dirName = Path.GetFileName (item);
            foreach (var fileName in Directory.GetFiles (item).Select (Path.GetFileName)) {
                var compactPath = $"{path}/{dirName}/{fileName}";

                Dictionary<string, SaveAuction> auctions = null;
                try {
                    auctions = FileController.LoadAs<Dictionary<string, SaveAuction>> (compactPath);

                } catch (Exception e) {

                    Console.WriteLine ($"Skipping {compactPath} because of {e.Message}");
                }
                if (auctions == null)
                    continue;

                foreach (var auction in auctions) {
                    completeAuctions.Add (auction.Value);
                }

                FileController.Move (compactPath, compactPath.Replace ("nauctions", "importedAuctions"));

                if (completeAuctions.Count > 1000)
                    break;
            }
            if (completeAuctions.Count > 1000)
                break;
        }
        return completeAuctions;

    }
}