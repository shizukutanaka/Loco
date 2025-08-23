import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  TouchableOpacity,
  Alert,
  Platform,
  Vibration
} from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Provider as PaperProvider, Card, Button, FAB, Portal, Modal } from 'react-native-paper';
import { BarCodeScanner } from 'expo-barcode-scanner';
import * as Notifications from 'expo-notifications';
import * as Location from 'expo-location';
import * as Device from 'expo-device';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Icon from 'react-native-vector-icons/MaterialCommunityIcons';
import QRCode from 'react-native-qrcode-svg';
import io from 'socket.io-client';

const Tab = createBottomTabNavigator();

// Configure notifications
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

// Home Screen - Quick Actions
function HomeScreen({ navigation }) {
  const [flows, setFlows] = useState([]);
  const [connected, setConnected] = useState(false);
  const [socket, setSocket] = useState(null);

  useEffect(() => {
    connectToServer();
    loadFlows();
    setupNotifications();
  }, []);

  const connectToServer = async () => {
    const serverUrl = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
    const ws = io(serverUrl);
    
    ws.on('connect', () => {
      setConnected(true);
      showNotification('Connected', 'Connected to Loco server');
    });

    ws.on('disconnect', () => {
      setConnected(false);
    });

    ws.on('flow-update', (data) => {
      loadFlows();
      showNotification('Flow Update', data.message);
    });

    setSocket(ws);
  };

  const loadFlows = async () => {
    try {
      const serverUrl = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
      const response = await fetch(`${serverUrl}/api/flows`);
      const data = await response.json();
      setFlows(data);
    } catch (error) {
      Alert.alert('Error', 'Failed to load flows');
    }
  };

  const executeFlow = async (flowId) => {
    try {
      Vibration.vibrate(50);
      const serverUrl = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
      const response = await fetch(`${serverUrl}/api/flows/${flowId}/execute`, {
        method: 'POST'
      });
      
      if (response.ok) {
        showNotification('Success', 'Flow executed successfully');
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to execute flow');
    }
  };

  const showNotification = async (title, body) => {
    await Notifications.scheduleNotificationAsync({
      content: {
        title,
        body,
        data: { timestamp: Date.now() },
      },
      trigger: null,
    });
  };

  const setupNotifications = async () => {
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('default', {
        name: 'default',
        importance: Notifications.AndroidImportance.MAX,
        vibrationPattern: [0, 250, 250, 250],
        lightColor: '#FF231F7C',
      });
    }

    if (Device.isDevice) {
      const { status: existingStatus } = await Notifications.getPermissionsAsync();
      let finalStatus = existingStatus;
      
      if (existingStatus !== 'granted') {
        const { status } = await Notifications.requestPermissionsAsync();
        finalStatus = status;
      }
      
      if (finalStatus !== 'granted') {
        Alert.alert('Permission needed', 'Failed to get push token for notifications!');
      }
    }
  };

  return (
    <ScrollView style={styles.container}>
      <Card style={styles.statusCard}>
        <Card.Title 
          title="Connection Status" 
          subtitle={connected ? 'Connected to Loco' : 'Disconnected'}
          left={(props) => <Icon {...props} name={connected ? 'wifi' : 'wifi-off'} />}
        />
      </Card>

      <Text style={styles.sectionTitle}>Quick Actions</Text>
      
      <View style={styles.quickActions}>
        <TouchableOpacity 
          style={styles.quickButton}
          onPress={() => navigation.navigate('Scanner')}
        >
          <Icon name="qrcode-scan" size={40} color="#007AFF" />
          <Text>Scan QR</Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={styles.quickButton}
          onPress={() => navigation.navigate('Triggers')}
        >
          <Icon name="lightning-bolt" size={40} color="#FF9500" />
          <Text>Triggers</Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={styles.quickButton}
          onPress={() => navigation.navigate('Location')}
        >
          <Icon name="map-marker" size={40} color="#4CD964" />
          <Text>Location</Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={styles.quickButton}
          onPress={() => navigation.navigate('Voice')}
        >
          <Icon name="microphone" size={40} color="#FF3B30" />
          <Text>Voice</Text>
        </TouchableOpacity>
      </View>

      <Text style={styles.sectionTitle}>Recent Flows</Text>
      
      {flows.slice(0, 5).map((flow) => (
        <Card key={flow.id} style={styles.flowCard}>
          <Card.Title title={flow.name} subtitle={flow.description} />
          <Card.Actions>
            <Button onPress={() => executeFlow(flow.id)}>Execute</Button>
            <Button onPress={() => navigation.navigate('FlowDetails', { flow })}>Details</Button>
          </Card.Actions>
        </Card>
      ))}

      <FAB
        style={styles.fab}
        icon="plus"
        onPress={() => navigation.navigate('CreateFlow')}
      />
    </ScrollView>
  );
}

// QR Scanner Screen
function ScannerScreen({ navigation }) {
  const [hasPermission, setHasPermission] = useState(null);
  const [scanned, setScanned] = useState(false);

  useEffect(() => {
    (async () => {
      const { status } = await BarCodeScanner.requestPermissionsAsync();
      setHasPermission(status === 'granted');
    })();
  }, []);

  const handleBarCodeScanned = async ({ type, data }) => {
    setScanned(true);
    Vibration.vibrate(100);
    
    // Check if it's a Loco flow URL
    if (data.includes('loco.app') || data.includes('LF-')) {
      Alert.alert(
        'Import Flow',
        `Import this flow?\n${data}`,
        [
          { text: 'Cancel', style: 'cancel' },
          { 
            text: 'Import', 
            onPress: async () => {
              try {
                const serverUrl = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
                const response = await fetch(`${serverUrl}/api/flows/import`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ url: data })
                });
                
                if (response.ok) {
                  Alert.alert('Success', 'Flow imported successfully');
                  navigation.goBack();
                }
              } catch (error) {
                Alert.alert('Error', 'Failed to import flow');
              }
            }
          }
        ]
      );
    } else {
      Alert.alert('QR Code', `Data: ${data}`);
    }
  };

  if (hasPermission === null) {
    return <Text>Requesting camera permission...</Text>;
  }
  if (hasPermission === false) {
    return <Text>No access to camera</Text>;
  }

  return (
    <View style={styles.container}>
      <BarCodeScanner
        onBarCodeScanned={scanned ? undefined : handleBarCodeScanned}
        style={StyleSheet.absoluteFillObject}
      />
      {scanned && (
        <Button mode="contained" onPress={() => setScanned(false)} style={styles.scanAgainButton}>
          Tap to Scan Again
        </Button>
      )}
    </View>
  );
}

// Mobile Triggers Screen
function TriggersScreen() {
  const [location, setLocation] = useState(null);
  const [triggers, setTriggers] = useState([
    { id: 1, name: 'When I arrive home', type: 'location', enabled: true },
    { id: 2, name: 'When battery is low', type: 'battery', enabled: false },
    { id: 3, name: 'When connected to WiFi', type: 'wifi', enabled: true },
    { id: 4, name: 'Shake to execute', type: 'shake', enabled: false },
  ]);

  useEffect(() => {
    setupLocationTracking();
  }, []);

  const setupLocationTracking = async () => {
    let { status } = await Location.requestForegroundPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission denied', 'Location permission is required for location triggers');
      return;
    }

    let location = await Location.getCurrentPositionAsync({});
    setLocation(location);

    // Watch location for geofencing
    Location.watchPositionAsync(
      {
        accuracy: Location.Accuracy.High,
        timeInterval: 10000,
        distanceInterval: 10,
      },
      (newLocation) => {
        checkLocationTriggers(newLocation);
      }
    );
  };

  const checkLocationTriggers = (location) => {
    // Check if user entered/left geofenced areas
    // This would connect to the server to check configured locations
  };

  const toggleTrigger = async (triggerId) => {
    const updatedTriggers = triggers.map(t => 
      t.id === triggerId ? { ...t, enabled: !t.enabled } : t
    );
    setTriggers(updatedTriggers);
    
    // Save to server
    const serverUrl = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
    await fetch(`${serverUrl}/api/triggers/${triggerId}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        enabled: updatedTriggers.find(t => t.id === triggerId).enabled 
      })
    });
  };

  return (
    <ScrollView style={styles.container}>
      <Card style={styles.statusCard}>
        <Card.Title 
          title="Current Location" 
          subtitle={location ? `${location.coords.latitude}, ${location.coords.longitude}` : 'Getting location...'}
          left={(props) => <Icon {...props} name="map-marker" />}
        />
      </Card>

      <Text style={styles.sectionTitle}>Mobile Triggers</Text>
      
      {triggers.map((trigger) => (
        <Card key={trigger.id} style={styles.flowCard}>
          <Card.Title 
            title={trigger.name}
            subtitle={`Type: ${trigger.type}`}
            left={(props) => (
              <Icon 
                {...props} 
                name={trigger.type === 'location' ? 'map-marker' : 
                      trigger.type === 'battery' ? 'battery' :
                      trigger.type === 'wifi' ? 'wifi' : 'gesture-swipe'}
              />
            )}
            right={() => (
              <Button 
                mode={trigger.enabled ? 'contained' : 'outlined'}
                onPress={() => toggleTrigger(trigger.id)}
              >
                {trigger.enabled ? 'ON' : 'OFF'}
              </Button>
            )}
          />
        </Card>
      ))}
    </ScrollView>
  );
}

// Settings Screen
function SettingsScreen() {
  const [serverUrl, setServerUrl] = useState('');
  const [showQR, setShowQR] = useState(false);

  useEffect(() => {
    loadSettings();
  }, []);

  const loadSettings = async () => {
    const url = await AsyncStorage.getItem('serverUrl') || 'http://192.168.1.100:5000';
    setServerUrl(url);
  };

  const saveSettings = async () => {
    await AsyncStorage.setItem('serverUrl', serverUrl);
    Alert.alert('Success', 'Settings saved');
  };

  const deviceInfo = {
    deviceId: Device.deviceName,
    platform: Platform.OS,
    version: Platform.Version,
    serverUrl: serverUrl
  };

  return (
    <ScrollView style={styles.container}>
      <Card style={styles.settingsCard}>
        <Card.Title title="Server Settings" />
        <Card.Content>
          <TextInput
            label="Server URL"
            value={serverUrl}
            onChangeText={setServerUrl}
            placeholder="http://192.168.1.100:5000"
            style={styles.input}
          />
        </Card.Content>
        <Card.Actions>
          <Button onPress={saveSettings}>Save</Button>
          <Button onPress={() => setShowQR(true)}>Show QR</Button>
        </Card.Actions>
      </Card>

      <Card style={styles.settingsCard}>
        <Card.Title title="Device Info" />
        <Card.Content>
          <Text>Device: {Device.deviceName}</Text>
          <Text>Platform: {Platform.OS} {Platform.Version}</Text>
          <Text>App Version: 0.0.1</Text>
        </Card.Content>
      </Card>

      <Portal>
        <Modal visible={showQR} onDismiss={() => setShowQR(false)} contentContainerStyle={styles.modal}>
          <Text style={styles.modalTitle}>Device Connection QR</Text>
          <View style={styles.qrContainer}>
            <QRCode
              value={JSON.stringify(deviceInfo)}
              size={200}
            />
          </View>
          <Button onPress={() => setShowQR(false)}>Close</Button>
        </Modal>
      </Portal>
    </ScrollView>
  );
}

// Main App Component
export default function App() {
  return (
    <PaperProvider>
      <NavigationContainer>
        <Tab.Navigator
          screenOptions={({ route }) => ({
            tabBarIcon: ({ focused, color, size }) => {
              let iconName;
              
              if (route.name === 'Home') {
                iconName = focused ? 'home' : 'home-outline';
              } else if (route.name === 'Scanner') {
                iconName = 'qrcode-scan';
              } else if (route.name === 'Triggers') {
                iconName = 'lightning-bolt';
              } else if (route.name === 'Settings') {
                iconName = 'cog';
              }
              
              return <Icon name={iconName} size={size} color={color} />;
            },
            tabBarActiveTintColor: '#007AFF',
            tabBarInactiveTintColor: 'gray',
          })}
        >
          <Tab.Screen name="Home" component={HomeScreen} />
          <Tab.Screen name="Scanner" component={ScannerScreen} />
          <Tab.Screen name="Triggers" component={TriggersScreen} />
          <Tab.Screen name="Settings" component={SettingsScreen} />
        </Tab.Navigator>
      </NavigationContainer>
    </PaperProvider>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
    padding: 10,
  },
  statusCard: {
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginVertical: 10,
    marginLeft: 5,
  },
  quickActions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-around',
    marginBottom: 20,
  },
  quickButton: {
    alignItems: 'center',
    padding: 15,
    margin: 5,
    backgroundColor: 'white',
    borderRadius: 10,
    width: '40%',
    elevation: 2,
  },
  flowCard: {
    marginBottom: 10,
  },
  fab: {
    position: 'absolute',
    margin: 16,
    right: 0,
    bottom: 0,
  },
  scanAgainButton: {
    position: 'absolute',
    bottom: 50,
    alignSelf: 'center',
  },
  settingsCard: {
    marginBottom: 10,
  },
  input: {
    marginBottom: 10,
  },
  modal: {
    backgroundColor: 'white',
    padding: 20,
    margin: 20,
    borderRadius: 10,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 20,
  },
  qrContainer: {
    alignItems: 'center',
    marginBottom: 20,
  },
});
