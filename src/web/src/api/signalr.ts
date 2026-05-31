import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'

export function usePhotosHub(onPhotosChanged: () => void): void {
  const callbackRef = useRef(onPhotosChanged)
  useEffect(() => {
    callbackRef.current = onPhotosChanged
  })

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/photos')
      .withAutomaticReconnect()
      .build()

    connection.on('PhotosChanged', () => callbackRef.current())
    connection.start().catch((err) => {
      console.warn('[signalr] connection failed', err)
    })

    return () => {
      void connection.stop()
    }
  }, [])
}
