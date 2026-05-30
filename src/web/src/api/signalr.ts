import { useEffect } from 'react'
import * as signalR from '@microsoft/signalr'

export function usePhotosHub(onPhotosChanged: () => void): void {
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/photos')
      .withAutomaticReconnect()
      .build()

    connection.on('PhotosChanged', onPhotosChanged)
    connection.start().catch((err) => {
      console.warn('[signalr] connection failed', err)
    })

    return () => {
      void connection.stop()
    }
  }, [onPhotosChanged])
}
