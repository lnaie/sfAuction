using System;
using static System.Console;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Fabric.Query;
using System.Fabric.Health;
using System.Collections;
using System.Web.Script.Serialization;
using SFAuction.Common;

namespace SFAuction.Management {
   internal static class Management {
      private static void Main(string[] args) {
         //const String clusterEndpoint = "mvpconftest2.westus.cloudapp.azure.com:19000";
         //const String clusterEndpoint = "localhost:19000";
         const String clusterEndpoint = "deintegrotestcluster.westus.cloudapp.azure.com:19000";
         var fc = new FabricClient(clusterEndpoint);

         foreach (var p in GetAllPartitionEndpointsAsync(fc, new Uri("fabric:/WordCount/WordCountService")).GetAwaiter().GetResult()) {
            Console.WriteLine(p.Key);
            foreach (var ep in p.Value) {
               Console.WriteLine("   " + ep.Address);
            }
         }
         DumpClusterHealthAsync(fc).GetAwaiter().GetResult();
         DumpClusterAsync(fc).GetAwaiter().GetResult();

         var appPkgPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\SFAuction.App\pkg\Debug");
         appPkgPath = Path.GetFullPath(appPkgPath);
         const String imageStorePath = "SFAuction";

         UploadAndProvisionAppAsync(fc, appPkgPath, imageStorePath).GetAwaiter().GetResult();
      }

      private static String GetImageStoreConnectionString(XElement clusterManifest) {
         XNamespace fabricNamespace = "http://schemas.microsoft.com/2011/01/fabric";

         var imageStoreConnectionString =
            (from sectionElement in clusterManifest.Element(fabricNamespace + "FabricSettings").Elements(fabricNamespace + "Section")
             let nameAttribute = sectionElement.Attribute("Name")
             where nameAttribute != null && nameAttribute.Value == "Management"
             from parameterElement in sectionElement.Elements(fabricNamespace + "Parameter")
             let parameterNameAttribute = parameterElement.Attribute("Name")
             where parameterNameAttribute != null && parameterNameAttribute.Value == "ImageStoreConnectionString"
             select parameterElement.Attribute("Value")).First().Value;
         return imageStoreConnectionString;
      }

      private static async Task UploadAndProvisionAppAsync(FabricClient fc, String localAppPackagePath, String imageStorePath) {
         var clusterManifestXml = await fc.ClusterManager.GetClusterManifestAsync();
         XElement clusterManifest = XElement.Parse(clusterManifestXml);

         // Copy the application package to the cluster's image store:
         String imageStoreConnectionString = GetImageStoreConnectionString(clusterManifest);
         fc.ApplicationManager.CopyApplicationPackage(imageStoreConnectionString, localAppPackagePath, imageStorePath);

         // Provision/Register the application:
         await fc.ApplicationManager.ProvisionApplicationAsync(imageStorePath);

         // After the app is provisioned/registered, we can delete it from the image store:
         fc.ApplicationManager.RemoveApplicationPackage(imageStoreConnectionString, imageStorePath);
      }

      private static async Task StartAppAsync(FabricClient fc) {
         // Create an instance of the application:
         Uri appName = new Uri(@"fabric:/SFAuction");
         await fc.ApplicationManager.CreateApplicationAsync(
            new ApplicationDescription(appName, "SFAuctionType", "1.0.0"));

         // Create instance of the 3 application services:
         Uri actorLookupSvcName = new Uri(appName.ToString() + "/ActorLookupSvc");
         await fc.ServiceManager.CreateServiceAsync(
            new StatefulServiceDescription {
               ApplicationName = appName, ServiceTypeName = "ActorLookupServiceType",
               ServiceName = actorLookupSvcName,
               //JMR: HasPersistedState = true, 
               PartitionSchemeDescription = new UniformInt64RangePartitionSchemeDescription(5),
               MinReplicaSetSize = 3, TargetReplicaSetSize = 3
            });
#if false
         Uri itemActorSvcName = new Uri(appName.ToString() + "/ItemActorSvc");
         await fc.ServiceManager.CreateServiceAsync(
            new StatefulServiceDescription {
               ApplicationName = appName, ServiceTypeName = "ItemActorServiceType",
               ServiceName = itemActorSvcName,
               //JMR: HasPersistedState = true, 
               PartitionSchemeDescription = new UniformInt64RangePartitionSchemeDescription(5),
               MinReplicaSetSize = 3, TargetReplicaSetSize = 3
            });

         Uri userActorSvcName = new Uri(appName.ToString() + "/UserActorSvc");
         await fc.ServiceManager.CreateServiceAsync(
            new StatefulServiceDescription {
               ApplicationName = appName, ServiceTypeName = "UserActorServiceType",
               ServiceName = userActorSvcName,
               //JMR: HasPersistedState = true, 
               PartitionSchemeDescription = new UniformInt64RangePartitionSchemeDescription(5),
               MinReplicaSetSize = 3, TargetReplicaSetSize = 3
            });
#endif
      }

      private static async Task<IDictionary<Guid, ICollection<ResolvedServiceEndpoint>>> GetAllPartitionEndpointsAsync(FabricClient fc, Uri serviceName) {
         var serviceEndpoints = new Dictionary<Guid, ICollection<ResolvedServiceEndpoint>>();
         var servicePartitionKeys = await GetAllPartitionKeysAsync(fc, serviceName);
         foreach (var pk in servicePartitionKeys) {
            var sp = await fc.ServiceManager.ResolveServicePartitionAsync(serviceName, pk.Value);
            serviceEndpoints.Add(pk.Key, sp.Endpoints);
         }
         return serviceEndpoints;
      }

      private static async Task<IDictionary<Guid, Int64>> GetAllPartitionKeysAsync(FabricClient fc, Uri serviceName) {
         var partitionAndKey = new Dictionary<Guid, Int64>();
         foreach (Partition p in await fc.QueryManager.GetPartitionListAsync(serviceName)) {
            Int64RangePartitionInformation pi = (Int64RangePartitionInformation)p.PartitionInformation;
            partitionAndKey.Add(pi.Id, pi.LowKey);
         }
         return partitionAndKey;
      }
      private static void WriteHealth(this HealthEvent he) {
         var hi = he.HealthInformation;
         WriteLine($"{he.SourceUtcTimestamp}, {he.LastModifiedUtcTimestamp}, {he.IsExpired}, {he.LastOkTransitionAt}, {he.LastWarningTransitionAt}, {he.LastErrorTransitionAt}");
         WriteLine($"State={hi.HealthState}, Source={hi.SourceId}, Property={hi.Property}, TTL={hi.TimeToLive}, RemoveWhenExpired={hi.RemoveWhenExpired}, Desc={hi.Description}, Seq={hi.SequenceNumber}");
      }
      private static async Task DumpClusterHealthAsync(FabricClient fc) {
         FabricClient.HealthClient hm = fc.HealthManager;

         ClusterHealth clusterHealth = await hm.GetClusterHealthAsync();
         WriteLine($"Cluster: State={clusterHealth.AggregatedHealthState}");
         foreach (HealthEvent healthEvent in clusterHealth.HealthEvents) {
            healthEvent.WriteHealth();
         }
         foreach (HealthEvaluation healthEval in clusterHealth.UnhealthyEvaluations) {
            WriteLine(healthEval);
         }
         foreach (NodeHealthState nodeHealth in clusterHealth.NodeHealthStates) {
            WriteLine($"Node: State={nodeHealth.AggregatedHealthState}, Name={nodeHealth.NodeName}");
         }
         foreach (ApplicationHealthState appHealthState in clusterHealth.ApplicationHealthStates) {
            WriteLine($"App: State={appHealthState.AggregatedHealthState}, Name={appHealthState.ApplicationName}");
         }


         //await hm.GetNodeHealthAsync()
         ApplicationHealth appHealth = await hm.GetApplicationHealthAsync(new Uri(@"fabric:/"));
         WriteLine($"App: State={appHealth.AggregatedHealthState}, Name={appHealth.ApplicationName}");
         foreach (HealthEvent healthEvent in appHealth.HealthEvents) {
            healthEvent.WriteHealth();
         }
      }
      private static async Task DumpClusterAsync(FabricClient fc) {
         var qm = fc.QueryManager;
         ClusterLoadInformation info = await qm.GetClusterLoadInformationAsync();
         WriteLine($"LB StartTime={info.LastBalancingStartTimeUtc}, LB EndTime={info.LastBalancingEndTimeUtc}");
         foreach (LoadMetricInformation lmi in info.LoadMetricInformationList)
            WriteLine($"Name={lmi.Name}");

         foreach (ProvisionedFabricCodeVersion pfcv in await qm.GetProvisionedFabricCodeVersionListAsync())
            WriteLine($"SF CodeVersion={pfcv.CodeVersion}");

         foreach (ProvisionedFabricConfigVersion pfcv in await qm.GetProvisionedFabricConfigVersionListAsync())
            WriteLine($"SF Config version={pfcv.ConfigVersion}");

         foreach (ApplicationType at in await qm.GetApplicationTypeListAsync()) {
            WriteLine($"Name={at.ApplicationTypeName}, Ver={at.ApplicationTypeVersion}");
            foreach (ServiceType st in await qm.GetServiceTypeListAsync(at.ApplicationTypeName, at.ApplicationTypeVersion))
               WriteLine($"   Name={st.ServiceManifestName}, Ver={st.ServiceManifestVersion}");
         }

         foreach (Node n in await qm.GetNodeListAsync()) {
            WriteLine($"Name={n.NodeName}, Health={n.HealthState}, FD={n.FaultDomain}, UD={n.UpgradeDomain}, IP={n.IpAddressOrFQDN}");
            NodeLoadInformation nli = await qm.GetNodeLoadInformationAsync(n.NodeName);
            foreach (NodeLoadMetricInformation nlmi in nli.NodeLoadMetricInformationList)
               WriteLine($"Name={nlmi.Name}, Remaining={nlmi.NodeRemainingCapacity}");

            foreach (DeployedApplication da in await qm.GetDeployedApplicationListAsync(n.NodeName)) {
               WriteLine(da.ApplicationName);
               foreach (DeployedCodePackage dcp in await qm.GetDeployedCodePackageListAsync(n.NodeName, da.ApplicationName)) {
                  WriteLine(dcp.EntryPoint.EntryPointLocation);
               }
               foreach (DeployedServiceType dst in await qm.GetDeployedServiceTypeListAsync(n.NodeName, da.ApplicationName)) {
                  WriteLine(dst.ServiceTypeName);
               }
               foreach (DeployedServiceReplica dsr in await qm.GetDeployedReplicaListAsync(n.NodeName, da.ApplicationName)) {
                  long? id = (dsr as DeployedStatefulServiceReplica)?.ReplicaId;
                  if (id == null) id = (dsr as DeployedStatelessServiceInstance)?.InstanceId;
                  WriteLine($"ServiceName={dsr.ServiceName}, PartitionId={dsr.Partitionid}, ReplicaId={id}, Status={dsr.ReplicaStatus}");
                  DeployedServiceReplicaDetail drd = await qm.GetDeployedReplicaDetailAsync(n.NodeName, dsr.Partitionid, id.Value);

               }
               foreach (DeployedServicePackage dsp in await qm.GetDeployedServicePackageListAsync(n.NodeName, da.ApplicationName)) {
                  WriteLine($"ManifestName={dsp.ServiceManifestName}, Status={dsp.DeployedServicePackageStatus}");
               }
            }
         }

         foreach (Application a in await qm.GetApplicationListAsync()) {
            WriteLine($"App={a.ApplicationName}, Status={a.ApplicationStatus}, Health={a.HealthState}");

            foreach (Service s in await qm.GetServiceListAsync(a.ApplicationName)) {
               WriteLine($"   Service={s.ServiceName}, Status={s.ServiceStatus}, Health={s.HealthState}");

               foreach (Partition p in await qm.GetPartitionListAsync(s.ServiceName)) {
                  WriteLine($"      Partition={p.PartitionInformation.Id}, Status={p.PartitionStatus}, Health={p.HealthState}");
                  PartitionLoadInformation pli = await qm.GetPartitionLoadInformationAsync(p.PartitionInformation.Id);

                  foreach (Replica r in await qm.GetReplicaListAsync(p.PartitionInformation.Id)) {
                     WriteLine($"         Replica={r.Id}, Status={r.ReplicaStatus}, Health={r.HealthState}");

                     ReplicaLoadInformation rli = await qm.GetReplicaLoadInformationAsync(p.PartitionInformation.Id, r.Id);
                  }
                  UnplacedReplicaInformation ur = await qm.GetUnplacedReplicaInformationAsync(s.ServiceName.ToString(), p.PartitionInformation.Id, false);
                  if (ur.UnplacedReplicaReasons.Count > 0) {
                     WriteLine("Unplaced partition replicas");
                     foreach (var reason in ur.UnplacedReplicaReasons) WriteLine(reason);
                  }
               }
            }
         }
      }
   }
}
