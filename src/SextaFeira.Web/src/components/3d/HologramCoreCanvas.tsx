import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

export type AgentVisualState = 'IDLE' | 'LISTENING' | 'THINKING' | 'SPEAKING' | 'EXECUTING';

interface HologramCoreCanvasProps {
  state: AgentVisualState;
  audioLevel?: number; // 0.0 to 1.0 from microphone or TTS
}

export const HologramCoreCanvas: React.FC<HologramCoreCanvasProps> = ({ state, audioLevel = 0 }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const stateRef = useRef(state);
  const audioLevelRef = useRef(audioLevel);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  useEffect(() => {
    audioLevelRef.current = audioLevel;
  }, [audioLevel]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene, Camera, Renderer
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    camera.position.z = 7;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(renderer.domElement);

    // 1. Holographic Central Core Orb (Wireframe + Shaded Sphere)
    const orbGeometry = new THREE.IcosahedronGeometry(1.8, 4);
    const orbMaterial = new THREE.MeshBasicMaterial({
      color: 0x00f0ff,
      wireframe: true,
      transparent: true,
      opacity: 0.7,
      blending: THREE.AdditiveBlending
    });
    const coreOrb = new THREE.Mesh(orbGeometry, orbMaterial);
    scene.add(coreOrb);

    // Inner Glowing Core
    const innerGeometry = new THREE.SphereGeometry(1.1, 32, 32);
    const innerMaterial = new THREE.MeshBasicMaterial({
      color: 0x0099ff,
      transparent: true,
      opacity: 0.5,
      blending: THREE.AdditiveBlending
    });
    const innerOrb = new THREE.Mesh(innerGeometry, innerMaterial);
    scene.add(innerOrb);

    // 2. Quantum Particle Field (Surrounding Cloud)
    const particleCount = 600;
    const particleGeometry = new THREE.BufferGeometry();
    const particlePositions = new Float32Array(particleCount * 3);
    const particleVelocities: THREE.Vector3[] = [];

    for (let i = 0; i < particleCount; i++) {
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(Math.random() * 2 - 1);
      const radius = 2.4 + Math.random() * 1.5;

      particlePositions[i * 3] = radius * Math.sin(phi) * Math.cos(theta);
      particlePositions[i * 3 + 1] = radius * Math.sin(phi) * Math.sin(theta);
      particlePositions[i * 3 + 2] = radius * Math.cos(phi);

      particleVelocities.push(
        new THREE.Vector3(
          (Math.random() - 0.5) * 0.02,
          (Math.random() - 0.5) * 0.02,
          (Math.random() - 0.5) * 0.02
        )
      );
    }

    particleGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3));
    const particleMaterial = new THREE.PointsMaterial({
      color: 0x00ffff,
      size: 0.06,
      transparent: true,
      opacity: 0.85,
      blending: THREE.AdditiveBlending
    });
    const particleField = new THREE.Points(particleGeometry, particleMaterial);
    scene.add(particleField);

    // 3. Concentric Holographic Radar & Data Rings
    const createRing = (radius: number, tubeRadius: number, color: number, rotationX: number, rotationY: number) => {
      const ringGeo = new THREE.TorusGeometry(radius, tubeRadius, 2, 80);
      const ringMat = new THREE.MeshBasicMaterial({
        color,
        wireframe: true,
        transparent: true,
        opacity: 0.5,
        blending: THREE.AdditiveBlending
      });
      const ringMesh = new THREE.Mesh(ringGeo, ringMat);
      ringMesh.rotation.x = rotationX;
      ringMesh.rotation.y = rotationY;
      return ringMesh;
    };

    const ring1 = createRing(2.7, 0.015, 0x00f0ff, Math.PI / 2, 0);
    const ring2 = createRing(3.2, 0.01, 0x0077ff, Math.PI / 3, Math.PI / 4);
    const ring3 = createRing(3.8, 0.012, 0x00f0ff, -Math.PI / 4, Math.PI / 6);
    scene.add(ring1);
    scene.add(ring2);
    scene.add(ring3);

    // Resize Handler
    const handleResize = () => {
      if (!container) return;
      const w = container.clientWidth;
      const h = container.clientHeight;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h);
    };
    window.addEventListener('resize', handleResize);

    // Animation Loop
    let animationFrameId: number;
    let clock = new THREE.Clock();

    const animate = () => {
      animationFrameId = requestAnimationFrame(animate);
      const elapsedTime = clock.getElapsedTime();
      const currentState = stateRef.current;
      const currentAudio = audioLevelRef.current;

      // Base rotations
      let baseSpeed = 0.4;
      let particleSpeed = 1.0;
      let targetScale = 1.0;

      // Dynamic reactive states according to the Prompt Mestre:
      if (currentState === 'IDLE') {
        baseSpeed = 0.3;
        targetScale = 1.0 + Math.sin(elapsedTime * 1.5) * 0.04;
        orbMaterial.color.setHex(0x00f0ff);
      } else if (currentState === 'LISTENING') {
        // Expanded diameter, reactive in real-time to microphone audio level
        baseSpeed = 0.6;
        targetScale = 1.15 + (currentAudio * 0.45) + Math.sin(elapsedTime * 6.0) * 0.08;
        orbMaterial.color.setHex(0x00ffff);
      } else if (currentState === 'THINKING' || currentState === 'EXECUTING') {
        // Particles accelerating around core in perpendicular axes (quantum simulation)
        baseSpeed = 1.8;
        particleSpeed = 4.0;
        targetScale = 1.05 + Math.sin(elapsedTime * 8.0) * 0.06;
        orbMaterial.color.setHex(0xffaa00);
      } else if (currentState === 'SPEAKING') {
        // Light waves and pulsation synchronized directly with TTS output
        baseSpeed = 0.8;
        targetScale = 1.1 + (currentAudio * 0.5) + Math.sin(elapsedTime * 10.0) * 0.12;
        orbMaterial.color.setHex(0x00f0ff);
      }

      // Smooth scaling interpolation
      coreOrb.scale.lerp(new THREE.Vector3(targetScale, targetScale, targetScale), 0.1);
      innerOrb.scale.lerp(new THREE.Vector3(targetScale * 0.9, targetScale * 0.9, targetScale * 0.9), 0.1);

      // Rotate Orb
      coreOrb.rotation.x = elapsedTime * baseSpeed * 0.6;
      coreOrb.rotation.y = elapsedTime * baseSpeed;
      innerOrb.rotation.y = -elapsedTime * baseSpeed * 0.8;

      // Rotate Rings in perpendicular gyroscopic axes
      ring1.rotation.z = elapsedTime * baseSpeed * 0.5;
      ring2.rotation.x = elapsedTime * baseSpeed * 0.4;
      ring2.rotation.z = -elapsedTime * baseSpeed * 0.3;
      ring3.rotation.y = elapsedTime * baseSpeed * 0.6;

      // Rotate and animate particle cloud
      particleField.rotation.y = elapsedTime * 0.15 * particleSpeed;
      particleField.rotation.x = Math.sin(elapsedTime * 0.2) * 0.3;

      renderer.render(scene, camera);
    };

    animate();

    return () => {
      cancelAnimationFrame(animationFrameId);
      window.removeEventListener('resize', handleResize);
      renderer.dispose();
      orbGeometry.dispose();
      orbMaterial.dispose();
      innerGeometry.dispose();
      innerMaterial.dispose();
      particleGeometry.dispose();
      particleMaterial.dispose();
      if (container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, []);

  return (
    <div 
      ref={containerRef} 
      style={{ 
        position: 'absolute', 
        top: 0, 
        left: 0, 
        width: '100%', 
        height: '100%', 
        zIndex: 5,
        pointerEvents: 'none'
      }} 
    />
  );
};
