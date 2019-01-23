# ARvis-CFD
This repo contains source code and testing data for the paper entitled "Improved Visualization of Indoor Thermal Environment on Mobile Devices based on Augmented Reality and Computational Fluid Dynamics"

Use the following data to reference this repo or the paper:

Lin, J., Cao, J., Zhang, J., van Treeck, C., and Frisch, J., Improved Visualization of Indoor Thermal Environment on Mobile Devices based on Augmented Reality and Computational Fluid Dynamics. Automation in Construction, 2019. (submitted)

## Background and Objectives
Augmented reality (AR) based visualization of computational fluid dynamics (CFD) simulation on mobile devices is important for the understanding and discussion of indoor thermal environments in the design process. However, utilizing AR-based mobile device for indoor thermal environment understanding still encounters problems due to limited computational power and lack of efficient interaction methods. 

The research aim at improving the performance of AR-based CFD visualization on a mobile device and providind the users with intuitive interaction with indoor environment.

## Contributions
To achieve the above mentioned goal, an integrated approach based on client-server framework is proposed. Within the approach:
1) A new mobile friendly data format (cfd4a) with low computational complexity is proposed for CFD simulation results representation. 
2) Server-side data pre-processing method is also introduced to reduce the computational power and time needed on the client side.
3) Interactive section view selection and time-step animation methods are proposed for intuitive interaction with the AR environment. 
4) A prototype system is developed with Unity3D engine and Tango Tablet.

## Results
Demonstrations and user feedback showed that the proposed method and prototype system provide an intuitive and fluent AR-based environment for interactive indoor thermal environment visualization. 
 
Comparing to the widely used vtk format, a data compression ratio of 63.4% and a loading time saving ratio of 89.3% are achieved in the performance test with the proposed method.

Standard RESTful API and widely used Unity3D engine are adopted, thus enabiling further extension to support other AR devices.

## Folder Structure
Bin: output folder for complied binary files and runnable applications.

Data: test data of an office room, scan folder for surface model generated from point cloud, section folder for different section views, and streamline folder for streamline represented air flows. Ending number of the file names are the simulation time-steps.

Documentation: collected documents to understand vtk files and paraview.

Src: source code folder, CfdHost for server-side service, Tango for client implementation, and VtkToolkit for data pre-processing.
