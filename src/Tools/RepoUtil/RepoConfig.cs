﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepoUtil
{
    /// <summary>
    /// Packages in the repo fall into the following groups:
    /// 
    /// Static Packages:
    /// 
    /// These are packages which should never change.  In other words if there was a scenario where a new version of the 
    /// package was available the reference should not update to the new version.  For all time it should remain at the 
    /// specified version.
    ///
    /// Because they are fixed it's possible to have multiple vesions of the same package.  For instance it's okay to 
    /// have many versions of Newtonsoft.Json referenced here because there is no need to unify.  Or at least it's stated
    /// that we don't need to unify.
    ///
    /// Floating Packages:
    /// 
    /// These are packages which are expected to change when new versions are available.  These are tools, dependencies, etc ...
    /// which are expected to evolve over time and we need to move forward with those dependencies. 
    /// 
    /// Generally these fall into two categories:
    ///
    ///     Build Dependencies
    ///     Toolset Dependencies
    ///
    /// This distinction is necessary to help break circular references for repos when constructing build graphs.
    /// </summary>
    internal class RepoConfig
    {
        /// <summary>
        /// Fixed references which do not change during a build.
        /// </summary>
        internal ImmutableArray<NuGetPackage> StaticPackages { get; }

        /// <summary>
        /// This is a map of static package names to the list of supported versions.
        /// </summary>
        internal ImmutableDictionary<string, ImmutableArray<string>> StaticPackagesMap { get; }

        internal ImmutableArray<string> ToolsetPackages { get; }

        internal RepoConfig(IEnumerable<NuGetPackage> staticPackages, IEnumerable<string> toolsetPackages)
        {
            StaticPackages = staticPackages.OrderBy(x => x.Name).ToImmutableArray();

            // TODO: Validate duplicate names in the floating lists
            ToolsetPackages = toolsetPackages.OrderBy(x => x).ToImmutableArray();

            var map = new Dictionary<string, List<string>>();
            foreach (var nugetRef in staticPackages)
            {
                List<string> list;
                if (!map.TryGetValue(nugetRef.Name, out list))
                {
                    list = new List<string>(capacity: 1);
                    map[nugetRef.Name] = list;
                }

                list.Add(nugetRef.Version);
            }

            StaticPackagesMap = ImmutableDictionary<string, ImmutableArray<string>>.Empty;
            foreach (var pair in map)
            {
                StaticPackagesMap = StaticPackagesMap.Add(pair.Key, pair.Value.ToImmutableArray());
            }
        }

        internal static RepoConfig ReadFrom(string jsonFilePath)
        {
            // Need to track any file that has dependencies
            var obj = JObject.Parse(File.ReadAllText(jsonFilePath));
            var staticPackages = (JObject)obj["staticPackages"];
            var staticPackagesList = ImmutableArray.CreateBuilder<NuGetPackage>();
            foreach (var prop in staticPackages.Properties())
            {
                if (prop.Value.Type == JTokenType.String)
                {
                    var version = (string)prop.Value;
                    var nugetRef = new NuGetPackage(prop.Name, version);
                    staticPackagesList.Add(nugetRef);
                }
                else
                {
                    foreach (var version in ((JArray)prop.Value).Values<string>())
                    {
                        var nugetRef = new NuGetPackage(prop.Name, version);
                        staticPackagesList.Add(nugetRef);
                    }
                }
            }

            var toolsetPackagesProp = obj.Property("toolsetPackages");
            var toolsetPackages = ((JArray)toolsetPackagesProp.Value).Values<string>();

            return new RepoConfig(
                staticPackagesList,
                toolsetPackages);
        }
    }
}
