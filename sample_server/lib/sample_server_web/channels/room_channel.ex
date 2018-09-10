defmodule SampleServerWeb.RoomChannel do
  use Phoenix.Channel
  require Logger

  def join("room:lobby", _message, socket) do
    {:ok, socket}
  end

  def join("room:" <> _private_room_id, _params, _socket) do
    {:error, %{reason: "unauthorized"}}
  end

  def handle_in("new_msg", %{"body" => body}, socket) do
    broadcast!(socket, "new_msg", %{body: body})
    {:reply, {:error, %{message: "sent"}}, socket}
  end
end
