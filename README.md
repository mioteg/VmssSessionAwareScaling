# Session Aware Azure VM Scale Set Scaling

The auto scaling features of Azure Virtual Machines Scale Sets only look at infrastructure level load metrics. That works well for stateless workloads with any of the instances in a VMSS capable of handling a request. For workloads with sessions affinity (sessions tied to a specific instance), this scaling mechanism could inadvertently deallocate instances that have active sessions or jobs, because the overall load is too low to maintain the instance.

You don't have to use auto scaling. A VMSS can also be scaled explicitly. This sample demonstrates how to use that to scale based on different metrics,such as sessions. 

DISCLAIMER: THIS IS A SAMPLE AND SHOULD NOT BE USED FOR PRODUCTION.
This code lacks at least the following:

1. Error handling
2. Logging
3. Persistence of the load and potentially other things that need to be persisted in case of restart of the scale management code.

## Known issues

This code has not been tested with [Large VM Scale Sets](https://docs.microsoft.com/en-us/azure/virtual-machine-scale-sets/virtual-machine-scale-sets-placement-groups).
The code working with Fault Domains may not work when the Scale Set is put in multiple placement groups.

## Architecture

The key components in this solution are the Capacity Manager (VmssCapacityManager) and the Load Manager (IVmssLoadManager). These two components interact to determine the load on the VMSS, and the available capacity.

### Capacity Manager

The Capacity Manager interacts with the underlying VMSS to get information about the number of active instances, and to add or delete instances as is needed to satisfy the demand.
The Capacity Manager gets the load on the individual instances from the Load Manager. Based on that information it calculates the needed capacity, and adjusts the VMSS ad needed.
If there are more instances than needed to satisfy the demand, it will mark the instances with the lowest load to no longer accept new load (e.g. sessions, jobs, etc.), and it will notify the Load Manager of this. The Load Manager is then responsible for not adding new load.
Once the load reaches 0 on an instance that was marked not to accept new load, the Capacity Manager removes that particular instance.

### Load Manager

The Load Manager is an interface that needs to be implemented to provide information about the load to the Capacity Manager. The implementation of the load manager depends on the way you measure load on an instance. This could be based on sessions, as is the case in the sample implementation, which basically acts as a session broker. It could also interact with a component on the the instances that provides information on the load.

The Load Manager must also be able to lock an instance so it won't accept any more load. In the code an instance that no longer accepts load is known as Closed. If additional capacity is needed, the Closed state may be removed, so it can accept load again.

## Sample Application

The sample application is a console that understands two commands:

1. add - this requests a session to be added.
2. remove - this removes a particular session. The parameter for ths command is the number of the session. The session list is shown after each command.

You can run the code using the VmssManager, which works with an existing VMSS. You can use the TestVmssManager instead to provide a local testbed.

### Setting up your environment

To use the sample application, you need to take the following actions:

1. [Create a Virtual Machine Scale Set](https://docs.microsoft.com/en-us/azure/virtual-machine-scale-sets/virtual-machine-scale-sets-portal-create)
2. [Create an App Registration in Azure](https://docs.microsoft.com/en-us/azure/active-directory/active-directory-app-registration)
3. [Give the App permissions on the Resource Group](https://docs.microsoft.com/en-us/azure/active-directory/role-based-access-control-configure) in which you created the Virtual Machine Scale Set, so it has permissions to make changes to the VMSS.
4. Put the following values in the Config.cs:

    a. DirectoryId - The tenant ID of the Azure Active Directory your subscription (and app) are tied to.
    
    b. ApplicationId - The Applicication ID of the App in Azure AD
    
    c. ApplicationKey - A key that you created as a password for the App. If you don't have one, go to Azure Ad -> App Registrations -> Your App -> Keys to create one.
    
    d. ResourceGroupName - The name of the resource group of the VMSS.
    
    e. VmssName - The name of the VMSS in the Resource Group.
