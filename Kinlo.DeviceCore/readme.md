DeviceCore
│
├── Abstractions                // 抽象契约层（核心接口）
│   ├── IDevice.cs
│   ├── IConnection.cs
│   ├── IProtocol.cs
│   ├── IMapping.cs
│   ├── IDeviceRuntime.cs
│
├── Runtime                     // 运行时核心（调度/生命周期）
│   ├── DeviceManager.cs
│   ├── DeviceHost.cs
│   ├── DeviceScheduler.cs
│   ├── PollingService.cs
│   ├── SubscriptionService.cs
│
├── Devices                     // 设备实现（具体设备类型）
│   ├── Plc
│   │   ├── OmronDevice.cs
│   │   ├── SiemensDevice.cs
│   │
│   ├── Sensor
│   │   ├── BarcodeScannerDevice.cs
│   │   ├── CameraDevice.cs
│
├── Protocols                   // 协议层（byte ↔ frame）
│   ├── Cip
│   │   ├── CipProtocol.cs
│   │   ├── CipFrame.cs
│   │
│   ├── Modbus
│   │   ├── ModbusTcpProtocol.cs
│
├── Connections                 // 传输层（IO）
│   ├── Tcp
│   │   ├── TcpConnection.cs
│   │
│   ├── Udp
│   │   ├── UdpConnection.cs
│   │
│   ├── Socket
│   │   ├── SocketConnection.cs
│
├── Mapping                     // ⭐ 语义映射层
│   ├── TagMapping.cs
│   ├── DeviceMapping.cs
│   ├── PlcMappingProfile.cs
│   ├── MappingEngine.cs
│
├── Converters                  // ⭐ 数据转换层（非常关键）
│   ├── ByteConverters.cs
│   ├── EndianConverter.cs
│   ├── BitConverterEx.cs
│   ├── CipDataConverter.cs
│   ├── ValueConverters.cs
│
├── Models                      // 内部模型（系统内部使用）
│   ├── ProtocolModels
│   │   ├── CipModels.cs
│   │
│   ├── MappingModels
│   │   ├── TagModel.cs
│   │   ├── DeviceStateModel.cs
│
├── Dtos                        // 对外数据结构（API/MES/ERP）
│   ├── DeviceStatusDto.cs
│   ├── TagValueDto.cs
│   ├── AlarmDto.cs
│
 