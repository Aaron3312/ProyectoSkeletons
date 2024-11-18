import React, { useState, useEffect } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, BarChart, Bar } from 'recharts';

const MetricsDashboard = () => {
  const [metricsHistory, setMetricsHistory] = useState([]);
  const [currentMetrics, setCurrentMetrics] = useState([]);

  useEffect(() => {
    const fetchMetrics = async () => {
      try {
        const mockWorldState = {
          agentStates: [
            {
              id: "0",
              state: {
                position: { x: 0, y: 0, z: 0 },
                has_cube: false,
                available_cubes: [],
                time: Date.now() / 1000
              }
            },
            {
              id: "1",
              state: {
                position: { x: 5, y: 0, z: 0 },
                has_cube: false,
                available_cubes: [],
                time: Date.now() / 1000
              }
            },
            {
              id: "2",
              state: {
                position: { x: 10, y: 0, z: 0 },
                has_cube: false,
                available_cubes: [],
                time: Date.now() / 1000
              }
            },
            {
              id: "3",
              state: {
                position: { x: 15, y: 0, z: 0 },
                has_cube: false,
                available_cubes: [],
                time: Date.now() / 1000
              }
            },
            {
              id: "4",
              state: {
                position: { x: 20, y: 0, z: 0 },
                has_cube: false,
                available_cubes: [],
                time: Date.now() / 1000
              }
            }
          ]
        };

        const response = await fetch('http://localhost:5000/get_metrics', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(mockWorldState),
        });
        const data = await response.json();
        
        if (data.metrics) {
          setCurrentMetrics(data.metrics);
          setMetricsHistory(prev => [...prev, {
            timestamp: new Date().toLocaleTimeString(),
            ...data.metrics.reduce((acc, metric) => ({
              ...acc,
              [`Agent ${metric.agent_id}`]: metric.cubes_delivered
            }), {})
          }]);
        }
      } catch (error) {
        console.error('Error fetching metrics:', error);
      }
    };

    const interval = setInterval(fetchMetrics, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="w-full max-w-6xl mx-auto p-2 space-y-2">
      <Card className="shadow-sm">
        <CardHeader className="py-2">
          <CardTitle className="text-xl">Robot Performance Dashboard</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {/* Line Chart */}
          <Card className="shadow-sm">
            <CardHeader className="py-1">
              <CardTitle className="text-sm">Cubos Entregados por Agente (Histórico)</CardTitle>
            </CardHeader>
            <CardContent className="p-2">
              <div className="h-[180px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={metricsHistory.slice(-60)} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="timestamp" tick={{fontSize: 10}} />
                    <YAxis tick={{fontSize: 10}} />
                    <Tooltip />
                    <Legend wrapperStyle={{fontSize: '10px'}} />
                    {currentMetrics.map((metric, index) => (
                      <Line
                        key={metric.agent_id}
                        type="monotone"
                        dataKey={`Agent ${metric.agent_id}`}
                        stroke={`hsl(${index * 137.508}deg, 70%, 50%)`}
                        strokeWidth={1.5}
                      />
                    ))}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </CardContent>
          </Card>

          {/* Grid for Bar Chart and Detailed Metrics */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-2">
            {/* Bar Chart */}
            <Card className="shadow-sm">
              <CardHeader className="py-1">
                <CardTitle className="text-sm">Eficiencia Actual por Agente</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <div className="h-[180px]">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={currentMetrics} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="agent_id" tick={{fontSize: 10}} />
                      <YAxis tick={{fontSize: 10}} />
                      <Tooltip />
                      <Legend wrapperStyle={{fontSize: '10px'}} />
                      <Bar dataKey="efficiency_ratio" fill="#8884d8" name="Eficiencia" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </CardContent>
            </Card>

            {/* Detailed Metrics Table */}
            <Card className="shadow-sm">
              <CardHeader className="py-1">
                <CardTitle className="text-sm">Métricas Detalladas</CardTitle>
              </CardHeader>
              <CardContent className="p-2">
                <div className="overflow-x-auto">
                  <table className="w-full text-xs">
                    <thead>
                      <tr className="border-b">
                        <th className="p-1 text-left">ID</th>
                        <th className="p-1 text-left">Cubos</th>
                        <th className="p-1 text-left">Dist</th>
                        <th className="p-1 text-left">Efic</th>
                        <th className="p-1 text-left">Tasa</th>
                      </tr>
                    </thead>
                    <tbody>
                      {currentMetrics.map((metric) => (
                        <tr key={metric.agent_id} className="border-b">
                          <td className="p-1">A{metric.agent_id}</td>
                          <td className="p-1">{metric.cubes_delivered}</td>
                          <td className="p-1">{metric.total_distance} cm</td>
                          <td className="p-1">{metric.efficiency_ratio}%</td>
                          <td className="p-1">{metric.delivery_rate.toFixed(1)} / minute</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </CardContent>
            </Card>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default MetricsDashboard;